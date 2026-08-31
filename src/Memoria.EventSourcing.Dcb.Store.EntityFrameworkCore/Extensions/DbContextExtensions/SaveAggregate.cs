using Memoria.Results;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

public static partial class DcbDbContextExtensions
{
    /// <summary>
    /// Appends an aggregate's uncommitted events, refusing if the condition's boundary has moved,
    /// then refreshes its snapshot.
    /// </summary>
    /// <typeparam name="T">The aggregate type.</typeparam>
    /// <param name="dcbDbContext">The context.</param>
    /// <param name="query">The consistency boundary. Part of the snapshot's identity.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="aggregate">The aggregate whose staged events are appended.</param>
    /// <param name="condition">The concurrency check, or null to append unconditionally.</param>
    /// <param name="maxEventsPerAppend">The batch limit.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The outcome of the append.</returns>
    /// <remarks>
    /// The snapshot is written after the append commits, and a failure to write it does not fail the
    /// result. The events are durable at that point, and a snapshot is a derived cache the next read
    /// rebuilds — reporting failure would invite a caller to retry an append that already succeeded,
    /// which is the more expensive mistake. The failure is recorded on the current activity.
    /// </remarks>
    public static async Task<Result> SaveAggregate<T>(this IDcbDbContext dcbDbContext, TagQuery query,
        IDcbAggregateId<T> aggregateId, T aggregate, AppendCondition? condition,
        int maxEventsPerAppend = DefaultMaxEventsPerAppend, CancellationToken cancellationToken = default)
        where T : IDcbAggregateRoot
    {
        ArgumentNullException.ThrowIfNull(aggregate);

        var appendResult = await dcbDbContext.SaveEvents([..aggregate.UncommittedEvents], condition,
            maxEventsPerAppend, cancellationToken);

        if (appendResult.IsNotSuccess)
        {
            return appendResult.Failure!;
        }

        if (aggregate.Version == 0)
        {
            return Result.Ok();
        }

        try
        {
            aggregate.LatestPosition = await dcbDbContext.GetLatestPosition(query,
                aggregate.EventTypeFilter, cancellationToken);

            await dcbDbContext.WriteSnapshot(aggregate.ToSnapshotEntity(query, aggregateId), cancellationToken);
        }
        catch (Exception exception)
        {
            dcbDbContext.ChangeTracker.Clear();
            DcbDiagnostics.AddException(exception, "Refresh Aggregate Snapshot", query);
        }

        return Result.Ok();
    }
}
