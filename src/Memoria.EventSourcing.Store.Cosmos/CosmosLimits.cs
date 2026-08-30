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
    /// It writes the event document and its aggregate-event link per event, plus one aggregate
    /// document for the whole batch — so 49 events fill it exactly and 50 overflow it.
    /// </remarks>
    public const int MaxUncommittedEventsPerAggregateSave = (MaxBatchOperations - 1) / 2;

    /// <summary>
    /// Splits a sequence into chunks that each fit inside one transactional batch.
    /// </summary>
    /// <remarks>
    /// Only for writes over events that are already durable, where a failure part-way is redone by
    /// the next read. Appending events cannot use this: splitting the batch would surrender the
    /// atomicity the sequence check depends on.
    /// </remarks>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The items to split.</param>
    /// <param name="chunkSize">Operations per batch. Defaults to the full batch.</param>
    /// <returns>The chunks, in order.</returns>
    public static IEnumerable<IReadOnlyList<T>> InBatches<T>(this IReadOnlyList<T> items,
        int chunkSize = MaxBatchOperations)
    {
        for (var start = 0; start < items.Count; start += chunkSize)
        {
            var length = Math.Min(chunkSize, items.Count - start);
            var chunk = new T[length];
            for (var offset = 0; offset < length; offset++)
            {
                chunk[offset] = items[start + offset];
            }

            yield return chunk;
        }
    }
}
