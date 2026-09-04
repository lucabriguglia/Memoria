using Memoria.Results;
using Microsoft.EntityFrameworkCore;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

public static partial class DcbDbContextExtensions
{
    /// <summary>
    /// Appends an aggregate's uncommitted events and writes its snapshot, as one transaction,
    /// refusing if the condition's boundary has moved.
    /// </summary>
    /// <typeparam name="T">The aggregate type.</typeparam>
    /// <param name="dcbDbContext">The context.</param>
    /// <param name="aggregateId">The aggregate identifier, which carries the boundary.</param>
    /// <param name="aggregate">The aggregate whose staged events are appended.</param>
    /// <param name="condition">The concurrency check, or null to append unconditionally.</param>
    /// <param name="maxEventsPerAppend">The batch limit.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The outcome. Either both the events and the snapshot were written, or neither was.</returns>
    /// <remarks>
    /// <para>
    /// The two writes share one transaction, matching what the streamed store's <c>SaveAggregate</c>
    /// does in a single <c>SaveChanges</c>. Committing them separately and reporting success when
    /// only the events landed would leave the aggregate readable by <c>SnapshotOrCreate</c> but
    /// invisible to <c>SnapshotOnly</c> and <c>SnapshotWithNewEvents</c>, with nothing telling the
    /// caller and nothing able to retry — the events are durable, so a retry would be refused by its
    /// own condition. Failing together keeps the retry valid, because nothing was committed.
    /// </para>
    /// <para>
    /// The snapshot's position comes from the events this append wrote, not from re-reading the
    /// boundary. A re-read after the commit could see somebody else's append and stamp the snapshot
    /// as having consumed an event it never applied, which a later
    /// <see cref="ReadMode.SnapshotWithNewEvents"/> would then skip.
    /// </para>
    /// </remarks>
    public static async Task<Result> SaveAggregate<T>(this IDcbDbContext dcbDbContext,
        IDcbAggregateId<T> aggregateId, T aggregate, AppendCondition? condition,
        int maxEventsPerAppend = DefaultMaxEventsPerAppend, CancellationToken cancellationToken = default)
        where T : IDcbAggregateRoot
    {
        const string operation = "Save Aggregate";

        ArgumentNullException.ThrowIfNull(aggregate);

        var events = aggregate.UncommittedEvents.ToArray();

        if (events.Length > maxEventsPerAppend)
        {
            return DcbStoreFailures.BatchLimitExceeded(operation, events.Length, maxEventsPerAppend);
        }

        // Nothing staged and nothing folded: there is neither an append nor a state worth recording.
        if (events.Length == 0 && aggregate.Version == 0)
        {
            return Result.Ok();
        }

        var affectedTags = AffectedTags(events, condition);

        try
        {
            var heads = await dcbDbContext.ClaimTagHeads(affectedTags, condition, cancellationToken);

            await using var transaction = await dcbDbContext.Database.BeginTransactionAsync(cancellationToken);

            var lastPosition = AppendCondition.NoEvents;

            if (events.Length > 0)
            {
                var appendResult = await dcbDbContext.AppendCore(events, condition, affectedTags, heads,
                    cancellationToken);

                if (appendResult.IsNotSuccess)
                {
                    return appendResult.Failure!;
                }

                lastPosition = appendResult.Value;
            }

            if (aggregate.Version > 0)
            {
                // The fold the caller started from may already be further along than this append,
                // when the aggregate handled nothing it just wrote.
                aggregate.LatestPosition = Math.Max(aggregate.LatestPosition, lastPosition);

                await dcbDbContext.WriteSnapshot(aggregate.ToSnapshotEntity(aggregateId), cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);

            return Result.Ok();
        }
        catch (Exception exception)
        {
            return await dcbDbContext.AppendFailure(exception, operation, condition, affectedTags,
                cancellationToken, aggregateId.Boundary);
        }
    }
}
