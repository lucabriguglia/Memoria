using Memoria.Results;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

public static partial class DcbDbContextExtensions
{
    /// <summary>
    /// Folds a boundary into an aggregate, applies a change to it, and appends whatever it staged —
    /// conditioned on the boundary not having moved in between.
    /// </summary>
    /// <typeparam name="T">The aggregate type.</typeparam>
    /// <param name="dcbDbContext">The context.</param>
    /// <param name="query">The consistency boundary.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="update">The change to apply.</param>
    /// <param name="maxEventsPerAppend">The batch limit.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// The updated aggregate, or a failure. A boundary that moved between the read and the append
    /// fails with <c>memoria/concurrency-conflict</c>; the caller retries by calling this again.
    /// </returns>
    /// <remarks>
    /// This is the whole read-decide-append cycle in one call, and the shape most decisions want:
    /// the position the fold reached becomes the append condition, so the decision is guarded by
    /// exactly the events it was made from.
    /// </remarks>
    public static async Task<Result<T>> UpdateAggregate<T>(this IDcbDbContext dcbDbContext,
        TagQuery query, IDcbAggregateId<T> aggregateId, Action<T> update,
        int maxEventsPerAppend = DefaultMaxEventsPerAppend, CancellationToken cancellationToken = default)
        where T : IDcbAggregateRoot, new()
    {
        ArgumentNullException.ThrowIfNull(update);

        // Read before folding, never after. The position is a claim about what the decision saw, and
        // an event landing between the two reads makes that claim false in one of two directions.
        // Read first and the fold may see more than the position admits, so the append is refused and
        // the caller retries. Read second and the position admits more than the fold saw, so the
        // append is accepted on a decision that never read the intervening event — a lost update the
        // condition exists to prevent and would have signed off on.
        var latestPosition = await dcbDbContext.GetLatestPosition(query, cancellationToken: cancellationToken);

        // It cannot come from the fold instead: the fold stops at the last event the aggregate's own
        // type filter accepted, which may be behind the boundary's true head even with nothing else
        // running, so conditioning on it would refuse every append that followed an event the
        // aggregate ignores.
        var aggregateResult = await dcbDbContext.GetInMemoryAggregate(query, aggregateId, cancellationToken);
        if (aggregateResult.IsNotSuccess)
        {
            return aggregateResult.Failure!;
        }

        var aggregate = aggregateResult.Value!;

        update(aggregate);

        var saveResult = await dcbDbContext.SaveAggregate(query, aggregateId, aggregate,
            new AppendCondition(query, latestPosition), maxEventsPerAppend, cancellationToken);

        return saveResult.IsNotSuccess ? saveResult.Failure! : aggregate;
    }
}
