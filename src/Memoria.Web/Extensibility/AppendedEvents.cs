using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Memoria.Web.Extensibility;

/// <summary>
/// Reads the log itself: every event appended, or every one of a single type, whatever boundary it
/// was written under.
/// </summary>
/// <remarks>
/// The companion to <see cref="BoundaryEvents"/>, and the other way round from it. There the
/// question is which events one model is folded from, so the store resolves a boundary and the rows
/// it returns are ordered and paged in memory. Here there is no boundary to resolve — the rows
/// wanted are the whole table, or the part of it carrying one type — so narrowing, counting,
/// ordering and paging are all the database's, and only the reading of a payload is shared.
/// <para>
/// Narrowing by type is a scan: the store carries no index on <c>EventType</c>, deliberately, so
/// that the optimiser cannot prefer one to the tag semi-join every real read goes through. Paying
/// for it here is the right way round — this is a page someone opened to look at the log, not a
/// read on the write path.
/// </para>
/// </remarks>
public static class AppendedEvents
{
    /// <summary>
    /// Reads one page of the log.
    /// </summary>
    /// <param name="context">The DCB store.</param>
    /// <param name="eventType">The binding key to narrow to, or null for every type.</param>
    /// <param name="text">Text the row's payload or one of its tags has to carry, or null for any row.</param>
    /// <param name="descending">Whether the newest come first.</param>
    /// <param name="page">The page asked for, from one.</param>
    /// <param name="size">The rows per page.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <remarks>
    /// Ordered by position, which is the order the store appended in and the table's own key, so
    /// every page is a walk along the key rather than a sort of the whole table on a date nothing
    /// indexes. The date is drawn beside each row all the same; a position is assigned at the moment
    /// the date is stamped, so the two orders only ever differ where a clock was adjusted between
    /// appends, and then the position is the truer account of what came first.
    /// <para>
    /// The payload is matched as it was written, which is the serialized event whole — so the text
    /// looked for reaches the property names as well as the values under them. That is the point of
    /// it: the log is read here to find out what was appended, and a reader who knows only that an
    /// order carried a certain reference should not have to know which property holds it.
    /// </para>
    /// <para>
    /// The tags are matched beside it, for the same reason and one more: a DCB event belongs to no
    /// stream, so a tag is the only handle a boundary has on it, and the thing a reader most often
    /// arrives here knowing. Either side is enough — a row is wanted if it carries the text in its
    /// payload or under one of its tags — and a row several of whose tags match is still one row.
    /// </para>
    /// <para>
    /// Those two and nothing else: a number is text like any other here, and narrows to the rows
    /// carrying it rather than also to the row that happens to sit at that position. A box that
    /// answered both had no way of saying which it had done, so a reader looking for a reference
    /// beginning <c>14</c> was handed the fourteenth row alongside the rows they asked for and could
    /// not tell the difference.
    /// </para>
    /// </remarks>
    public static async Task<StoredEvents> Page(
        IDcbDbContext context,
        string? eventType,
        string? text,
        bool descending,
        int page,
        int size,
        TotalsCache? totals = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stored = context.DcbEvents.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(eventType))
            {
                stored = stored.Where(appended => appended.EventType == eventType);
            }

            if (!string.IsNullOrWhiteSpace(text))
            {
                // Lowered on both sides rather than with a provider's case-insensitive operator, so
                // this reads the same against SQL Server as it does against Postgres. Contains
                // rather than the Like the instances filter is built on: this text is typed against
                // a payload, where % and _ are ordinary characters someone may well be looking for,
                // and Contains leaves the provider to escape them rather than reading them as
                // wildcards. The tags are matched the same way, so one box means one thing across
                // the two columns it reaches.
                var wanted = text.Trim().ToLower();

                // Any rather than a join, so a row whose tags match twice is still one row: the
                // count under the title and the page beneath it both come off this query, and a row
                // shown twice would be a row another page is missing.
                stored = stored.Where(appended =>
                    appended.Data.ToLower().Contains(wanted) ||
                    appended.Tags.Any(tag => tag.Tag.ToLower().Contains(wanted)));
            }

            // Remembered for a while when there is somewhere to remember it: the count is the
            // dearer of a page's two queries, and the same for every page of one narrowing.
            var total = totals is null
                ? await stored.CountAsync(cancellationToken)
                : await totals.Total(
                    TotalsCache.KeyOf("dcb-events", eventType, text),
                    () => stored.CountAsync(cancellationToken));
            var placed = InstanceQuery.Place(page, total, size);

            var ordered = descending
                ? stored.OrderByDescending(appended => appended.Position)
                : stored.OrderBy(appended => appended.Position);

            var rows = await ordered
                .Skip(placed.Skip)
                .Take(size)
                .Select(appended => new
                {
                    appended.Position,
                    appended.EventType,
                    appended.Data,
                    appended.CreatedDate,
                    appended.CreatedBy,

                    // Projected with the row rather than included, so the tags of one page's worth
                    // are read and no navigation is left to be walked after the page is materialised.
                    // Ordered here because the (Tag, Position) key orders the join by tag anyway and
                    // the column is read down: a tag should be in the same place on every row.
                    Tags = appended.Tags.Select(tag => tag.Tag).OrderBy(tag => tag).ToList()
                })
                .ToListAsync(cancellationToken);

            // The same reading a boundary's events go through, so a row says the same thing
            // wherever it is met — including a row whose type the uploaded assemblies no longer
            // describe, which is listed rather than dropped. The tags and who appended it come with
            // it, as they do on the page about one event: a row on this page opens on that page, and
            // a row on a model's events tab opens in a sheet that lists the same facts.
            var read = rows
                .Select(row => BoundaryEvents.Read(context.TypeBindings, row.Position, row.EventType, row.Data, row.CreatedDate,
                    row.Tags, row.CreatedBy))
                .ToList();

            return new StoredEvents(read, total, placed.Page, placed.TotalPages, Error: null);
        }
        catch (Exception exception)
        {
            return new StoredEvents([], Total: 0, Page: 1, TotalPages: 1, Error: exception.Message);
        }
    }

    /// <summary>
    /// Reads the one event at an exact position, tags and all, or nothing when there is none there.
    /// </summary>
    /// <param name="context">The DCB store.</param>
    /// <param name="position">Where in the log the row sits, which is what makes it unique.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <remarks>
    /// The read a page about a single event is built on: no narrowing, no order and no count,
    /// because a position reaches one row or none. Nothing at it is no row and no error — a stale
    /// link to a row since gone is a fact about the log rather than a failure to read it.
    /// <para>
    /// The tags come with the row and in the same order the log's own page lists them, so a tag met
    /// on the list and met again on the page about the row is in the same place both times.
    /// </para>
    /// </remarks>
    public static async Task<ReadEvent> One(
        IDcbDbContext context,
        long position,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var row = await context.DcbEvents.AsNoTracking()
                .Where(appended => appended.Position == position)
                .Select(appended => new
                {
                    appended.Position,
                    appended.EventType,
                    appended.Data,
                    appended.CreatedDate,
                    appended.CreatedBy,
                    Tags = appended.Tags.Select(tag => tag.Tag).OrderBy(tag => tag).ToList()
                })
                .FirstOrDefaultAsync(cancellationToken);

            return new ReadEvent(
                row is null
                    ? null
                    : BoundaryEvents.Read(context.TypeBindings, row.Position, row.EventType, row.Data, row.CreatedDate, row.Tags,
                        row.CreatedBy),
                Error: null);
        }
        catch (Exception exception)
        {
            return new ReadEvent(Event: null, exception.Message);
        }
    }
}

/// <summary>The outcome of reading one event out of the DCB log.</summary>
/// <param name="Event">What is stored at that position, or null when nothing is.</param>
/// <param name="Error">Why the log could not be read, or null when it was.</param>
public sealed record ReadEvent(StoredEvent? Event, string? Error);
