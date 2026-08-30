using Microsoft.Azure.Cosmos;

namespace Memoria.EventSourcing.Store.Cosmos.Extensions;

/// <summary>
/// Request options for the store's own writes.
/// </summary>
/// <remarks>
/// <para>
/// Cosmos DB returns the written document in the response body by default. Every write in this store
/// discards it — the batch paths read only <c>IsSuccessStatusCode</c> and the upsert paths only
/// <c>StatusCode</c> — so for documents dominated by a serialised <c>data</c> payload that is roughly
/// double the write-path network volume for nothing. Turning the body off does not change the
/// request charge; it saves bytes on the wire and the deserialisation that would follow.
/// </para>
/// <para>
/// Set per request rather than through <see cref="CosmosClientOptions.EnableContentResponseOnWrite"/>
/// on the shared client, for two reasons: a consumer who replaces
/// <c>CosmosOptions.ClientOptions</c> would otherwise silently lose it, and a consumer writing
/// through <see cref="CosmosClientProvider.Client"/> would silently gain it.
/// </para>
/// </remarks>
internal static class WriteRequestOptions
{
    /// <summary>
    /// Options for a single-item write. The Cosmos DB SDK treats request options as read-only, so one
    /// shared instance is safe.
    /// </summary>
    public static readonly ItemRequestOptions Item = new() { EnableContentResponseOnWrite = false };

    /// <summary>
    /// Options for one item inside a transactional batch.
    /// </summary>
    public static readonly TransactionalBatchItemRequestOptions BatchItem =
        new() { EnableContentResponseOnWrite = false };
}
