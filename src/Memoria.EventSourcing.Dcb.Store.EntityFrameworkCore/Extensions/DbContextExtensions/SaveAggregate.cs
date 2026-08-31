using Memoria.Results;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

public static partial class DcbDbContextExtensions
{
    /// <summary>
    /// Appends an aggregate's uncommitted events, refusing if the condition's boundary has moved.
    /// </summary>
    /// <typeparam name="T">The aggregate type.</typeparam>
    /// <param name="dcbDbContext">The context.</param>
    /// <param name="aggregate">The aggregate whose staged events are appended.</param>
    /// <param name="condition">The concurrency check, or null to append unconditionally.</param>
    /// <param name="maxEventsPerAppend">The batch limit.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The outcome.</returns>
    /// <remarks>
    /// The snapshot is not written here — that arrives with the rest of the snapshot support. An
    /// aggregate is fully reconstructible from its boundary without one, so this is a complete
    /// append rather than half of one.
    /// </remarks>
    public static Task<Result> SaveAggregate<T>(this IDcbDbContext dcbDbContext, T aggregate,
        AppendCondition? condition, int maxEventsPerAppend = DefaultMaxEventsPerAppend,
        CancellationToken cancellationToken = default) where T : IDcbAggregateRoot
    {
        ArgumentNullException.ThrowIfNull(aggregate);

        return dcbDbContext.SaveEvents([..aggregate.UncommittedEvents], condition, maxEventsPerAppend,
            cancellationToken);
    }
}
