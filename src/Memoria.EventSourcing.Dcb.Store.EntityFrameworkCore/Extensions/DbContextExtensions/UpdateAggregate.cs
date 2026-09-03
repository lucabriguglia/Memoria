using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.Results;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

public static partial class DcbDbContextExtensions
{
    /// <summary>
    /// Brings an aggregate's snapshot up to date with the events appended inside its boundary since
    /// it was written, and persists the result.
    /// </summary>
    /// <typeparam name="T">The aggregate type.</typeparam>
    /// <param name="dcbDbContext">The context.</param>
    /// <param name="aggregateId">The aggregate identifier, which carries the boundary.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// The refreshed aggregate, or null when there is nothing to refresh — no snapshot and no events
    /// inside the boundary that this aggregate applies.
    /// </returns>
    /// <remarks>
    /// The counterpart of the streamed store's <c>UpdateAggregate</c>, and the same operation
    /// <see cref="ReadMode.SnapshotWithNewEvents"/> performs: read the latest snapshot, fold the
    /// events that arrived after it, write it back. It appends nothing and takes no
    /// <see cref="AppendCondition"/> — for a decision that produces events, read the boundary, fold
    /// it, and call <c>SaveAggregate</c> or <c>SaveEvents</c> with a condition.
    /// </remarks>
    public static async Task<Result<T?>> UpdateAggregate<T>(this IDcbDbContext dcbDbContext,
        IDcbAggregateId<T> aggregateId, CancellationToken cancellationToken = default)
        where T : IDcbAggregateRoot, new()
    {
        const string operation = "Update Aggregate";

        try
        {
            var snapshot = await dcbDbContext.GetSnapshotEntity(DcbSnapshotEntity.AggregateKind,
                aggregateId.ToStoreId(), aggregateId.Boundary, cancellationToken);

            // Starting from a fresh model when there is no snapshot is what lets this build one, and
            // is what the streamed store does.
            var aggregate = snapshot is null ? new T() : snapshot.ToAggregate<T>();
            aggregate.Tags = aggregateId.Boundary.Tags;

            return await dcbDbContext.RefreshAggregate(aggregateId, aggregate,
                snapshotExists: snapshot is not null, cancellationToken);
        }
        catch (Exception exception)
        {
            dcbDbContext.ChangeTracker.Clear();
            DcbDiagnostics.AddException(exception, operation, aggregateId.Boundary);
            return DcbStoreFailures.StorageFailure(operation, aggregateId.Boundary);
        }
    }
}
