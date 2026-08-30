using Microsoft.Azure.Cosmos;

namespace Memoria.EventSourcing.Store.Cosmos;

/// <summary>
/// The indexing policy the store is built for.
/// </summary>
/// <remarks>
/// <para>
/// The Cosmos DB default indexes every path of every document, including the serialised
/// <c>data</c> payload — the largest property, and one no query here can filter on, since
/// <c>eventPropertyFilter</c> compiles to <c>CONTAINS</c> and <c>CONTAINS</c> never uses an index.
/// This policy indexes only the paths the store filters or sorts on.
/// </para>
/// <para>
/// Measured against the Cosmos DB emulator over 200 event documents, it takes about 2.4% off writes
/// and 3–6% off reads. It defines no composite indexes: three were drafted, and they added roughly
/// 7% to every write while returning nothing, because these queries are single-partition with an
/// equality filter on <c>documentType</c> and an <c>ORDER BY c.sequence</c>, which the range index
/// on <c>/sequence</c> already serves.
/// </para>
/// <para>
/// The same policy ships as JSON under <c>scripts/install</c> for containers provisioned outside
/// the application. A test compares the two so they cannot drift.
/// </para>
/// </remarks>
public static class CosmosIndexingPolicy
{
    /// <summary>
    /// The paths the store filters or sorts on.
    /// </summary>
    /// <remarks>
    /// <c>/id</c> is deliberately absent: Cosmos DB always indexes it and rejects a policy that
    /// tries to override it. <c>/streamId</c> is deliberately present even though every query
    /// already scopes itself with a partition key — excluding it more than doubles the charge for
    /// the <c>MAX(sequence)</c> aggregate that guards every save.
    /// </remarks>
    private static readonly string[] IndexedPaths =
    [
        "/streamId/?",
        "/documentType/?",
        "/sequence/?",
        "/createdDate/?",
        "/aggregateId/?",
        "/appliedDate/?",
        "/eventType/?"
    ];

    /// <summary>
    /// Creates the recommended indexing policy.
    /// </summary>
    /// <returns>A new policy. <see cref="IndexingPolicy"/> is mutable, so each call returns its own.</returns>
    public static IndexingPolicy CreateRecommended()
    {
        var policy = new IndexingPolicy
        {
            IndexingMode = IndexingMode.Consistent,
            Automatic = true
        };

        policy.IncludedPaths.Clear();
        foreach (var path in IndexedPaths)
        {
            policy.IncludedPaths.Add(new IncludedPath { Path = path });
        }

        policy.ExcludedPaths.Clear();
        policy.ExcludedPaths.Add(new ExcludedPath { Path = "/*" });
        policy.ExcludedPaths.Add(new ExcludedPath { Path = "/\"_etag\"/?" });

        return policy;
    }
}
