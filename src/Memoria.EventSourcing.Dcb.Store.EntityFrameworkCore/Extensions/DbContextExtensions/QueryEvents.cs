using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

public static partial class DcbDbContextExtensions
{
    /// <summary>
    /// The events inside a boundary that a model applies, as a query still open to be counted,
    /// narrowed, ordered or paged in the database.
    /// </summary>
    /// <param name="dcbDbContext">The store.</param>
    /// <param name="query">The consistency boundary.</param>
    /// <param name="eventTypeFilter">The event types the model applies, or null for all of them.</param>
    /// <remarks>
    /// Which rows are inside a boundary is the store's own question, and the store answers it
    /// whole because that is what a fold needs. A tool reading about a boundary rather than folding
    /// it — a page of its events, a count of them — wants the same selection with the rest of the
    /// question asked of the database too, so this hands the selection over before it is run.
    /// Untracked, like every read the store makes of its own log.
    /// </remarks>
    public static IQueryable<DcbEventEntity> QueryEvents(this IDcbDbContext dcbDbContext,
        TagQuery query, Type[]? eventTypeFilter = null) =>
        dcbDbContext.Inside(query).ApplyEventTypeFilter(eventTypeFilter, dcbDbContext.TypeBindings);
}
