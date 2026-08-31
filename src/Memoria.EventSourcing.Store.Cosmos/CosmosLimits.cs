namespace Memoria.EventSourcing.Store.Cosmos;

/// <summary>
/// Cosmos DB limits the store has to work within.
/// </summary>
internal static class CosmosLimits
{
    /// <summary>
    /// The most operations Cosmos DB accepts in one transactional batch.
    /// </summary>
    public const int MaxBatchOperations = 100;

    /// <summary>
    /// The most events <c>SaveEvents</c> can append in one call: one document each.
    /// </summary>
    public const int MaxEventsPerSave = MaxBatchOperations;

    /// <summary>
    /// The most uncommitted events <c>SaveAggregate</c> can commit in one call.
    /// </summary>
    /// <remarks>
    /// One event document each, plus one aggregate document for the whole batch — so 99 events fill
    /// it exactly and 100 overflow it. Before 1.7.0 this was 49: each event also cost a link
    /// document, halving what one save could carry.
    /// </remarks>
    public const int MaxUncommittedEventsPerAggregateSave = MaxBatchOperations - 1;
}
