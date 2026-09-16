using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

public static partial class DcbDbContextExtensions
{
    /// <summary>
    /// Gets the headers of the stored events inside a boundary, in position order: where each
    /// sits, what it was stored as, and when it was appended — without its payload.
    /// </summary>
    /// <param name="dcbDbContext">The database context.</param>
    /// <param name="query">The tag query selecting the boundary.</param>
    /// <param name="eventTypeFilter">The event types to keep, or null to keep every type.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The headers, oldest first.</returns>
    /// <remarks>
    /// For a reader that needs to know the shape of a history — how many events, at which
    /// positions, of which types — rather than what they carry: the payloads are what make a
    /// boundary heavy, and a count or a place in the history needs none of them.
    /// </remarks>
    public static Task<List<DcbEventHeader>> GetEventHeaders(this IDcbDbContext dcbDbContext,
        TagQuery query, Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default) =>
        dcbDbContext.Inside(query)
            .ApplyEventTypeFilter(eventTypeFilter, dcbDbContext.TypeBindings)
            .OrderBy(eventEntity => eventEntity.Position)
            .Select(eventEntity => new DcbEventHeader(eventEntity.Position, eventEntity.EventType, eventEntity.CreatedDate))
            .ToListAsync(cancellationToken);
}
