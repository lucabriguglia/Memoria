using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.Cosmos.Documents;
using Memoria.Results;
using Microsoft.Azure.Cosmos;

namespace Memoria.EventSourcing.Store.Cosmos.Extensions;

/// <summary>
/// Writes an aggregate snapshot.
/// </summary>
/// <remarks>
/// One document, one upsert, whatever the length of the stream behind it. Until 1.7.0 this also
/// wrote a link document per event, which could overflow a transactional batch and so had to be
/// split across several — with an argument about why a partial write was recoverable. The links are
/// gone and that machinery went with them.
///
/// <para>
/// The write is an upsert because a snapshot is rebuilt over events that are already durable: a
/// rebuild has to replace what is there rather than collide with it, which a <c>CreateItem</c> would
/// reject as a conflict.
/// </para>
/// </remarks>
internal static class SnapshotWriteExtensions
{
    public static async Task<Result> WriteAggregateSnapshot<T>(this Container container,
        IStreamId streamId,
        IAggregateId<T> aggregateId,
        AggregateDocument aggregateDocument,
        string operation,
        CancellationToken cancellationToken) where T : IAggregateRoot
    {
        try
        {
            var response = await container.UpsertItemAsync(aggregateDocument, new PartitionKey(streamId.Id),
                WriteRequestOptions.Item, cancellationToken);
            DiagnosticsExtensions.AddActivityEvent(response, streamId, aggregateId, operation);

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
