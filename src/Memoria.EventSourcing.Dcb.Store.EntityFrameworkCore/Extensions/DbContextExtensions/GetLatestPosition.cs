using Microsoft.EntityFrameworkCore;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

public static partial class DcbDbContextExtensions
{
    /// <summary>
    /// Gets the latest position inside a boundary — the value an <see cref="AppendCondition"/> is
    /// built from.
    /// </summary>
    /// <param name="dcbDbContext">The context.</param>
    /// <param name="query">The consistency boundary.</param>
    /// <param name="eventTypeFilter">An optional filter on event type.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// The highest position inside the boundary, or <see cref="AppendCondition.NoEvents"/> when it
    /// is empty. An append conditioned on an empty boundary is the check for a decision that may
    /// only happen once, so the empty case has to be a real value rather than an absence.
    /// </returns>
    public static async Task<long> GetLatestPosition(this IDcbDbContext dcbDbContext,
        TagQuery query, Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default) =>
        await dcbDbContext.Inside(query)
            .ApplyEventTypeFilter(eventTypeFilter)
            .MaxAsync(eventEntity => (long?)eventEntity.Position, cancellationToken)
        ?? AppendCondition.NoEvents;
}
