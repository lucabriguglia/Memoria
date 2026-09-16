using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Filtering;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;
using Memoria.Web.Data;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace Memoria.Web.Extensibility;

/// <summary>
/// Reads the streamed log itself: every event appended, or every one of a single stream, a single
/// type, or both.
/// </summary>
/// <remarks>
/// The streamed counterpart of <see cref="AppendedEvents"/>, and the same shape as it — narrowing,
/// counting, ordering and paging are all the database's, and only the reading of a payload is
/// shared. What is here and not there is the stream: DCB writes an event into one log and works out
/// afterwards which boundaries take it in, while a streamed event is appended to a named stream and
/// carries that name, so the stream is a column of the table and something to narrow it by.
/// </remarks>
public static class StreamedEvents
{
    /// <summary>
    /// Reads one page of the log.
    /// </summary>
    /// <param name="context">The streamed store.</param>
    /// <param name="streamPattern">
    /// The pattern the ids of one stream type match, or null for every stream. Worked out by
    /// <see cref="IdShape"/>, because the log stores the id a stream produced and not the type
    /// that produced it — so narrowing to a type is matching the shape of what it writes.
    /// </param>
    /// <param name="eventType">The binding key to narrow to, or null for every type.</param>
    /// <param name="text">
    /// Text the row has to carry, in its stream, its own id or its payload, or null for any row.
    /// </param>
    /// <param name="descending">Whether the newest come first.</param>
    /// <param name="page">The page asked for, from one.</param>
    /// <param name="size">The rows per page.</param>
    /// <param name="eventTypes">
    /// The binding keys of the types a model applies, or null for every type. A set rather than the
    /// one key above it, because a model folds several and a reader narrows within them.
    /// </param>
    /// <param name="properties">
    /// The properties an event must carry to be one model's, or null for every event. An
    /// identifier's own filter, matched the way the store matches it when it folds.
    /// </param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <remarks>
    /// The stream and the sequence break a tie on the date: everything appended in one transaction
    /// is stamped from one clock reading and so shares a date exactly, and an unstable order under
    /// paging would show one row on two pages and another on none. The sequence alone would not
    /// settle it — it counts within a stream, so two streams both hold a first event — which is why
    /// the stream comes before it, and reads each stream's share of that moment in the order it was
    /// written.
    /// <para>
    /// The payload is matched as it was written, which is the serialized event whole — so the text
    /// looked for reaches the property names as well as the values under them. That is the point of
    /// it: the log is read here to find out what was appended, and a reader who knows only that an
    /// order carried a certain reference should not have to know which property holds it.
    /// </para>
    /// <para>
    /// The two ids the row carries are matched by the same text, which is why one box does for all
    /// three. The stream type narrows to a kind of stream — every customer — and what a reader wants
    /// next is one of them, or one row of one, both of which they have in front of them in the first
    /// two columns; a control apiece to type them into would be three ways of asking the one
    /// question this box already asks. Any of them matching is enough, because a reader typing an
    /// order reference means the payload, one typing <c>c-8d89</c> means the stream and one typing
    /// <c>:14</c> means the row, and nothing is served by making them say which.
    /// </para>
    /// </remarks>
    public static async Task<StoredStreamEvents> Page(
        StreamedStoreDbContext context,
        string? streamPattern,
        string? eventType,
        string? text,
        bool descending,
        int page,
        int size,
        IReadOnlyList<string>? eventTypes = null,
        IReadOnlyDictionary<string, string>? properties = null,
        long? beforeSequence = null,
        TotalsCache? totals = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stored = Narrow(context, streamPattern, eventType, text, eventTypes, properties, beforeSequence);

            // Remembered for a while when there is somewhere to remember it: the count is the
            // dearer of a page's two queries, and the same for every page of one narrowing.
            var total = totals is null
                ? await stored.CountAsync(cancellationToken)
                : await totals.Total(
                    TotalsCache.KeyOf("streamed-events", streamPattern, eventType, text, eventTypes, properties, beforeSequence),
                    () => stored.CountAsync(cancellationToken));
            var placed = InstanceQuery.Place(page, total, size);

            var ordered = Ordered(stored, descending);

            var read = await ReadRows(context.TypeBindings, ordered, placed.Skip, size, cancellationToken);

            return new StoredStreamEvents(read, total, placed.Page, placed.TotalPages, Error: null);
        }
        catch (Exception exception)
        {
            return new StoredStreamEvents([], Total: 0, Page: 1, TotalPages: 1, Error: exception.Message);
        }
    }

    /// <summary>
    /// Counts the events a narrowing leaves, across every page of them, without reading any.
    /// </summary>
    public static async Task<EventCount> Count(
        StreamedStoreDbContext context,
        string? streamPattern,
        string? eventType,
        string? text,
        IReadOnlyList<string>? eventTypes = null,
        IReadOnlyDictionary<string, string>? properties = null,
        long? beforeSequence = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stored = Narrow(context, streamPattern, eventType, text, eventTypes, properties, beforeSequence);

            return new EventCount(await stored.CountAsync(cancellationToken), Error: null);
        }
        catch (Exception exception)
        {
            return new EventCount(null, exception.Message);
        }
    }

    /// <summary>
    /// Reads the one event at a place in the narrowed log, in the order asked for, without counting
    /// the rest. A place past the end is nothing rather than a fault.
    /// </summary>
    public static async Task<PlacedStreamEvent> At(
        StreamedStoreDbContext context,
        string? streamPattern,
        string? eventType,
        string? text,
        bool descending,
        int index,
        IReadOnlyList<string>? eventTypes = null,
        IReadOnlyDictionary<string, string>? properties = null,
        long? beforeSequence = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stored = Narrow(context, streamPattern, eventType, text, eventTypes, properties, beforeSequence);
            var read = await ReadRows(context.TypeBindings, Ordered(stored, descending), index, take: 1, cancellationToken);

            return new PlacedStreamEvent(read.FirstOrDefault(), Error: null);
        }
        catch (Exception exception)
        {
            return new PlacedStreamEvent(null, exception.Message);
        }
    }

    /// <summary>
    /// Reads where every narrowed event sits in its stream, oldest first, and nothing else about it.
    /// </summary>
    /// <remarks>
    /// The sequences alone, in sequence order whatever order a page is drawn in: what places the
    /// rows of a narrowed table in a model's whole history. One read the size of the history's
    /// key column, rather than a count per row.
    /// </remarks>
    public static async Task<EventHistory> History(
        StreamedStoreDbContext context,
        string? streamPattern,
        string? eventType,
        string? text,
        IReadOnlyList<string>? eventTypes = null,
        IReadOnlyDictionary<string, string>? properties = null,
        long? beforeSequence = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stored = Narrow(context, streamPattern, eventType, text, eventTypes, properties, beforeSequence);
            var positions = await stored
                .OrderBy(appended => appended.Sequence)
                .Select(appended => (long)appended.Sequence)
                .ToListAsync(cancellationToken);

            return new EventHistory(positions, Error: null);
        }
        catch (Exception exception)
        {
            return new EventHistory(null, exception.Message);
        }
    }

    /// <summary>
    /// Reads the one event under an exact address, or nothing when there is none there.
    /// </summary>
    /// <param name="context">The streamed store.</param>
    /// <param name="address">The stream and the key.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <remarks>
    /// Matched on both halves of the address even though the key alone is the table's primary key:
    /// the store builds the key out of the stream, but how it does so is its business, and a page
    /// that was handed a stream and a key together should read the row that carries both. Nothing
    /// under the address is no row and no error, as a missing snapshot is.
    /// </remarks>
    public static async Task<ReadStreamEvent> One(
        StreamedStoreDbContext context,
        StreamedEventAddress address,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Its own projection rather than the one a page of rows goes through, because this is
            // the one read that wants the audit column: a table draws no column of who appended a
            // row, and the page about one row does.
            var row = await context.Events.AsNoTracking()
                .Where(appended => appended.StreamId == address.StreamId && appended.Id == address.Id)
                .Select(appended => new
                {
                    appended.Id, appended.StreamId, appended.Sequence, appended.EventType,
                    appended.Data, appended.CreatedDate, appended.CreatedBy
                })
                .FirstOrDefaultAsync(cancellationToken);

            return new ReadStreamEvent(
                row is null
                    ? null
                    : new StoredStreamEvent(
                        row.StreamId,
                        row.Id,
                        BoundaryEvents.Read(context.TypeBindings, row.Sequence, row.EventType, row.Data, row.CreatedDate,
                            writtenBy: row.CreatedBy)),
                Error: null);
        }
        catch (Exception exception)
        {
            return new ReadStreamEvent(Event: null, exception.Message);
        }
    }

    /// <summary>
    /// The events a narrowing leaves, as a query the three reads share: paged, counted, or one
    /// row at a place.
    /// </summary>
    private static IQueryable<EventEntity> Narrow(
        StreamedStoreDbContext context,
        string? streamPattern,
        string? eventType,
        string? text,
        IReadOnlyList<string>? eventTypes,
        IReadOnlyDictionary<string, string>? properties,
        long? beforeSequence)
    {
        var stored = context.Events.AsNoTracking();

        if (beforeSequence is { } bound)
        {
            // Below rather than at or below: what is asked is which event a row follows, and
            // the row itself is not the answer.
            stored = stored.Where(appended => appended.Sequence < bound);
        }

        if (!string.IsNullOrWhiteSpace(streamPattern))
        {
            // Like rather than a prefix comparison, because a stream's values are not always at
            // the end of its id: one built from two of them writes something after the first,
            // and the pattern has a hole in the middle to match it.
            stored = stored.Where(appended => EF.Functions.Like(appended.StreamId, streamPattern));
        }

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            stored = stored.Where(appended => appended.EventType == eventType);
        }

        if (eventTypes is { Count: > 0 })
        {
            // The keys the model applies, held against the key the row was written under. A
            // model's own filter is a list of CLR types; what the store holds is what each of
            // them is bound as, which is the only thing a row and a type have in common.
            var applied = eventTypes.ToList();

            stored = stored.Where(appended => applied.Contains(appended.EventType));
        }

        if (properties is { Count: > 0 })
        {
            foreach (var (name, value) in properties)
            {
                // The needle the store's own fold looks for, built the same way: the property
                // name and the value as JSON would have written them, matched against the
                // payload as text. Reproduced rather than shared because the store reaches it
                // through a provider-specific filter chosen at registration, and this reads a
                // column it was handed.
                var needle = $"{JsonConvert.ToString(name)}:{EventPropertyFilterValue.ToJsonLiteral(value)}";

                stored = stored.Where(appended => appended.Data.Contains(needle));
            }
        }

        if (!string.IsNullOrWhiteSpace(text))
        {
            // Lowered on both sides rather than with a provider's case-insensitive operator, so
            // this reads the same against SQL Server as it does against Postgres. Contains
            // rather than the Like the stream pattern is matched by: this text was typed by
            // someone, and % and _ are ordinary characters they may well be looking for, so
            // Contains leaves the provider to escape them rather than reading them as
            // wildcards.
            var wanted = text.Trim().ToLower();

            // The row's own key as well as the stream it names and the payload it carries. The
            // stream is looked in separately even though the key is built out of it: how the
            // store puts a key together is its business, and a filter that leaned on that would
            // quietly stop finding streams if it ever changed.
            stored = stored.Where(appended =>
                appended.StreamId.ToLower().Contains(wanted) ||
                appended.Id.ToLower().Contains(wanted) ||
                appended.Data.ToLower().Contains(wanted));
        }


        return stored;
    }

    /// <summary>The narrowed events in the order a page is read in, newest or oldest first.</summary>
    private static IOrderedQueryable<EventEntity> Ordered(IQueryable<EventEntity> stored, bool descending) =>
        descending
            ? stored.OrderByDescending(appended => appended.CreatedDate)
                .ThenBy(appended => appended.StreamId)
                .ThenByDescending(appended => appended.Sequence)
            : stored.OrderBy(appended => appended.CreatedDate)
                .ThenBy(appended => appended.StreamId)
                .ThenBy(appended => appended.Sequence);

    /// <summary>
    /// Reads a run of ordered rows and gives each the reading the DCB log and a boundary's events
    /// go through, so a row says the same thing wherever it is met.
    /// </summary>
    private static async Task<List<StoredStreamEvent>> ReadRows(
        TypeBindingSet bindings, IOrderedQueryable<EventEntity> ordered, int skip, int take, CancellationToken cancellationToken)
    {
        var rows = await ordered
            .Skip(skip)
            .Take(take)
            .Select(appended => new
            {
                appended.Id, appended.StreamId, appended.Sequence, appended.EventType,
                appended.Data, appended.CreatedDate, appended.CreatedBy
            })
            .ToListAsync(cancellationToken);

        // The same reading the DCB log and a boundary's events go through, so a row says the
        // same thing wherever it is met — including a row whose type the uploaded assemblies no
        // longer describe, which is listed rather than dropped. The sequence goes where a
        // position goes: both are the number that says where in the log a row sits, counted
        // within a stream here and across the whole log there. Who appended it comes too: a row
        // opened over the table says so, the same as the page about one event.
        var read = rows
            .Select(row => new StoredStreamEvent(
                row.StreamId,
                row.Id,
                BoundaryEvents.Read(bindings, row.Sequence, row.EventType, row.Data, row.CreatedDate,
                    writtenBy: row.CreatedBy)))
            .ToList();


        return read;
    }
}

/// <summary>One page of the streamed log.</summary>
/// <param name="Events">Those on this page, in the order asked for.</param>
/// <param name="Total">How many there are, across every page.</param>
/// <param name="Page">The page these are, from one.</param>
/// <param name="TotalPages">How many pages there are, never fewer than one.</param>
/// <param name="Error">Why the log could not be read, or null when it was.</param>
public sealed record StoredStreamEvents(
    IReadOnlyList<StoredStreamEvent> Events, int Total, int Page, int TotalPages, string? Error)
{
    /// <summary>
    /// Why this page is ordered more coarsely than it asked for, or null when it is not.
    /// </summary>
    /// <remarks>
    /// Not an error: the rows are real and the page is drawn. It says the tie-breakers were dropped,
    /// which a reader paging through a log should know, because two events sharing a timestamp can
    /// then move between pages. Only a store that cannot serve the full order sets it, which today
    /// means a Cosmos container without the composite index that order needs.
    /// </remarks>
    public string? OrderingNotice { get; init; }
}

/// <summary>
/// One appended event, and the stream it was appended to.
/// </summary>
/// <param name="StreamId">The stream it was written into.</param>
/// <param name="Id">
/// The key the store wrote the row under. The stream and the sequence joined, which is what makes
/// one event unique: a sequence counts within a stream, so neither half identifies a row on its own.
/// </param>
/// <param name="Event">
/// The event itself, read exactly as the DCB log reads one — so a payload, a type it cannot resolve
/// and a version are all shown the same way on both pages.
/// </param>
public sealed record StoredStreamEvent(string StreamId, string Id, StoredEvent Event);

