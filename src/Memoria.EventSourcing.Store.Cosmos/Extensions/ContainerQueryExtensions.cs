using Memoria.EventSourcing.Domain;
using Memoria.Results;
using Microsoft.Azure.Cosmos;

namespace Memoria.EventSourcing.Store.Cosmos.Extensions;

internal static class ContainerQueryExtensions
{
    public static async Task<Result<List<T>>> QueryListAsync<T>(this Container container,
        QueryDefinition queryDefinition, IStreamId streamId, string operation,
        CancellationToken cancellationToken = default)
    {
        var documents = new List<T>();

        try
        {
            using var iterator = container.GetItemQueryIterator<T>(queryDefinition, requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(streamId.Id)
            });

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                // FeedResponse<T> is not ICollection<T>, so AddRange cannot size the list from it.
                documents.EnsureCapacity(documents.Count + response.Count);
                documents.AddRange(response);
                response.AddActivityEvent(streamId, operation);
            }
        }
        catch (Exception ex)
        {
            DiagnosticsExtensions.AddException(ex, streamId, operation);
            return StoreFailures.StorageFailure(operation, streamId);
        }

        return documents;
    }
}
