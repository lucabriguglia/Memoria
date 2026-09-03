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
    /// <remarks>
    /// <para>
    /// Unfiltered, this never touches the events table. A tag row and the event it tags are written
    /// by the same transaction, so they become visible together and the highest position carrying a
    /// tag <em>is</em> the highest position inside the boundary — which makes this a backward seek on
    /// the <c>(Tag, Position)</c> key per tag rather than a semi-join against the log.
    /// </para>
    /// <para>
    /// That is the shape the append path takes: every conditioned append runs this inside its
    /// transaction, while holding the tag head rows every overlapping append contends on, so what it
    /// costs is what every other append over those tags waits for.
    /// </para>
    /// <para>
    /// A filter on event type is the exception, because only the events table records the type.
    /// </para>
    /// </remarks>
    public static async Task<long> GetLatestPosition(this IDcbDbContext dcbDbContext,
        TagQuery query, Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default)
    {
        if (eventTypeFilter is not { Length: > 0 })
        {
            return await dcbDbContext.PositionsInside(query)
                .MaxAsync(position => (long?)position, cancellationToken)
                ?? AppendCondition.NoEvents;
        }

        return await dcbDbContext.Inside(query)
            .ApplyEventTypeFilter(eventTypeFilter)
            .MaxAsync(eventEntity => (long?)eventEntity.Position, cancellationToken)
            ?? AppendCondition.NoEvents;
    }
}
