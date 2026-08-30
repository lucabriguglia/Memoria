using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.Cosmos.Documents;
using Memoria.Results;
using Microsoft.Azure.Cosmos;

namespace Memoria.EventSourcing.Store.Cosmos.Extensions;

/// <summary>
/// Writes an aggregate snapshot and the links from it to the events it was built from.
/// </summary>
/// <remarks>
/// <para>
/// Cosmos DB commits at most <see cref="CosmosLimits.MaxBatchOperations"/> operations per
/// transactional batch, and this write is one document per event plus the snapshot — so a stream
/// longer than that cannot be snapshotted in a single batch. Unlike appending events, splitting is
/// safe here: the events are already durable, so nothing is lost if only part of the write lands.
/// </para>
/// <para>
/// Two things make a partial write recoverable rather than permanent:
/// </para>
/// <list type="bullet">
/// <item>
/// The link documents go first and the snapshot last. A failure part-way therefore leaves no
/// snapshot, so the next read treats the aggregate as cold and does the work again, rather than
/// trusting a snapshot whose links are incomplete.
/// </item>
/// <item>
/// Every write is an upsert. A retry rewrites link documents that already exist, which a
/// <c>CreateItem</c> would reject as a conflict — turning a transient failure into one no retry
/// could clear.
/// </item>
/// </list>
/// </remarks>
internal static class SnapshotWriteExtensions
{
    public static async Task<Result> WriteAggregateSnapshot<T>(this Container container,
        IStreamId streamId,
        IAggregateId<T> aggregateId,
        AggregateDocument aggregateDocument,
        IReadOnlyList<EventDocument> eventDocuments,
        DateTimeOffset appliedDate,
        string operation,
        CancellationToken cancellationToken) where T : IAggregateRoot
    {
        var partitionKey = new PartitionKey(streamId.Id);
        var aggregateStoreId = aggregateId.ToStoreId();

        try
        {
            foreach (var chunk in eventDocuments.InBatches())
            {
                var batch = container.CreateTransactionalBatch(partitionKey);

                foreach (var eventDocument in chunk)
                {
                    batch.UpsertItem(new AggregateEventDocument
                    {
                        Id = $"{aggregateStoreId}|{eventDocument.Id}",
                        StreamId = streamId.Id,
                        AggregateId = aggregateStoreId,
                        EventId = eventDocument.Id,
                        AppliedDate = appliedDate
                    }, WriteRequestOptions.BatchItem);
                }

                var batchResponse = await batch.ExecuteAsync(cancellationToken);
                batchResponse.AddActivityEvent(streamId, aggregateId, operation);
                if (!batchResponse.IsSuccessStatusCode)
                {
                    return StoreFailures.StorageFailure(operation, streamId);
                }
            }

            // Last, so that a snapshot existing implies its links do too.
            var response = await container.UpsertItemAsync(aggregateDocument, partitionKey,
                WriteRequestOptions.Item, cancellationToken);
            response.AddActivityEvent(streamId, aggregateId, operation);

            return response.StatusCode is System.Net.HttpStatusCode.OK or System.Net.HttpStatusCode.Created
                ? Result.Ok()
                : StoreFailures.StorageFailure(operation, streamId);
        }
        catch (Exception ex)
        {
            DiagnosticsExtensions.AddException(ex, streamId, operation);
            return StoreFailures.StorageFailure(operation, streamId);
        }
    }
}
