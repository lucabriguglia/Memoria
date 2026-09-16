using Memoria.EventSourcing;
using Memoria.EventSourcing.Dcb;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;
using Memoria.EventSourcing.Domain;
using Microsoft.EntityFrameworkCore;

namespace Memoria.Web.Extensibility;

/// <summary>
/// Reads the events an aggregate or a projection applies out of the boundary it is folded from.
/// </summary>
/// <remarks>
/// Which rows are inside a boundary is the store's own question — one correlated <c>EXISTS</c> over
/// each event's tags, and different again for an intersection — so the selection comes from
/// <c>QueryEvents</c> rather than being written a second time here. What is added is the
/// narrowing to what a reader asked for, the ordering and paging over what that leaves, and the
/// reading: the log stores a binding key and a payload, and the page wants the event those name.
/// <para>
/// These are the events the model is built from, so the count agrees with the version a fold of
/// them would reach. A boundary may hold others — a wider model's events, sharing a tag — and those
/// are not listed here, because they are not what this model is made of.
/// </para>
/// </remarks>
public static class BoundaryEvents
{
    /// <summary>
    /// Counts the events a model applies inside a boundary, without reading any of them.
    /// </summary>
    /// <param name="context">The DCB store.</param>
    /// <param name="boundary">The consistency boundary.</param>
    /// <param name="applies">The event types the model applies, or null for all of them.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <remarks>
    /// The one number the info tab reads a version against. A count and nothing else: a page of
    /// one would read the whole boundary, payloads and all, to learn the same number.
    /// </remarks>
    public static async Task<EventCount> Count(
        IDcbDbContext context,
        TagQuery boundary,
        Type[]? applies,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return new EventCount(
                await context.QueryEvents(boundary, applies).CountAsync(cancellationToken), Error: null);
        }
        catch (Exception exception)
        {
            return new EventCount(null, exception.Message);
        }
    }

    /// <summary>
    /// Reads one page of the events a model applies inside a boundary.
    /// </summary>
    /// <param name="context">The DCB store.</param>
    /// <param name="boundary">The consistency boundary.</param>
    /// <param name="applies">The event types the model applies, or null for all of them.</param>
    /// <param name="eventType">The binding key to narrow to, or null for every type it applies.</param>
    /// <param name="text">Text the row's payload has to carry, or null for any row.</param>
    /// <param name="descending">Whether the newest come first.</param>
    /// <param name="page">The page asked for, from one.</param>
    /// <param name="size">The rows per page.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <remarks>
    /// Narrowed, counted, ordered and cut to the page in the database, so the payloads read are the
    /// page's and no more — the payloads are what make a boundary heavy, and a page of ten was
    /// reading every one of them. Narrowed before it is counted, so the count and the pager answer
    /// for what a reader asked for rather than for what the boundary holds.
    /// <para>
    /// The whole history is still asked for once, as positions alone: every row's version is its
    /// place in it, and narrowing or paging hides rows without renumbering the ones left. That is
    /// one read the size of the key column, which is what makes it affordable.
    /// <paramref name="applies"/> stays the store's — which events the model is made of is what
    /// the boundary read selects on, and it is not a reader's to widen.
    /// </para>
    /// <para>
    /// Position breaks a tie on the date. Events appended in one transaction are stamped from one
    /// clock reading and so share a date exactly, and an unstable order under paging would show one
    /// row on two pages and another on none.
    /// </para>
    /// </remarks>
    public static async Task<StoredEvents> Load(
        IDcbDbContext context,
        TagQuery boundary,
        Type[]? applies,
        string? eventType,
        string? text,
        bool descending,
        int page,
        int size,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var applied = context.QueryEvents(boundary, applies);

            var positions = await applied
                .OrderBy(row => row.Position)
                .Select(row => row.Position)
                .ToListAsync(cancellationToken);

            var matching = Matching(applied, eventType, text);

            // The positions already say how many there are when nothing narrowed them.
            var total = ReferenceEquals(matching, applied)
                ? positions.Count
                : await matching.CountAsync(cancellationToken);

            var placed = InstanceQuery.Place(page, total, size);

            var ordered = descending
                ? matching.OrderByDescending(row => row.CreatedDate).ThenByDescending(row => row.Position)
                : matching.OrderBy(row => row.CreatedDate).ThenBy(row => row.Position);

            // The tags projected with the row rather than included, as the log's own page reads
            // them, and ordered so a tag sits in the same place on every row. They selected the
            // boundary, and they are also a fact of each row inside it: the sheet a row opens in
            // over the table lists them, and a row may carry tags beyond the ones the boundary
            // asked by. Who appended it comes for the same sheet.
            var rows = await ordered
                .Skip(placed.Skip)
                .Take(size)
                .Select(row => new
                {
                    row.Position,
                    row.EventType,
                    row.Data,
                    row.CreatedDate,
                    row.CreatedBy,
                    Tags = row.Tags.Select(tag => tag.Tag).OrderBy(tag => tag).ToList()
                })
                .ToListAsync(cancellationToken);

            var read = rows
                .Select(row => Read(context.TypeBindings, row.Position, row.EventType, row.Data, row.CreatedDate, row.Tags, row.CreatedBy))
                .ToList();

            return new StoredEvents(read, total, placed.Page, placed.TotalPages, Error: null)
            {
                Versions = Versions(positions)
            };
        }
        catch (Exception exception)
        {
            return new StoredEvents([], Total: 0, Page: 1, TotalPages: 1, Error: exception.Message);
        }
    }

    /// <summary>
    /// The event types a model applies.
    /// </summary>
    /// <param name="model">The aggregate or projection type.</param>
    /// <param name="loaded">One already read, or null to build a fresh one to ask.</param>
    /// <returns>The types, or null when it applies every event inside its boundary.</returns>
    /// <remarks>
    /// The filter is an instance property, so something has to exist to be asked. The one already
    /// read answers for itself; failing that a fresh one is built, which is sound because the filter
    /// is a property of the type rather than of any state it holds.
    /// <para>
    /// A type that cannot be built — no parameterless constructor, which the store's own reads
    /// require and so would fail on too — narrows nothing rather than narrowing to nothing. Showing
    /// every event in the boundary is the wrong answer in a way a reader can see; showing none is
    /// the wrong answer in a way that looks like an empty log.
    /// </para>
    /// </remarks>
    public static Type[]? AppliedBy(Type model, object? loaded)
    {
        if (loaded is EventSourcedModel already)
        {
            return already.EventTypeFilter;
        }

        try
        {
            return InstanceFactory.CreateInstance(model) as EventSourcedModel is { } fresh
                ? fresh.EventTypeFilter
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Which version of the model each row produced: its place in the whole history by position,
    /// counted from one.
    /// </summary>
    /// <remarks>
    /// Over every position in the history rather than the page's: the fold applies the boundary in
    /// position order, and narrowing or paging hides rows without renumbering the ones left.
    /// </remarks>
    private static IReadOnlyDictionary<long, int> Versions(IReadOnlyList<long> positions) =>
        positions.Select((position, index) => (Position: position, Version: index + 1))
            .ToDictionary(placed => placed.Position, placed => placed.Version);

    /// <summary>
    /// Reads the shape of a model's whole history: the header of every event in its boundary that
    /// it applies, in position order, as the store folds them.
    /// </summary>
    /// <param name="context">The DCB store's context.</param>
    /// <param name="boundary">The tag query the model's identifier selects its events with.</param>
    /// <param name="applies">The event types the model applies, or null when it applies everything.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <remarks>
    /// Headers rather than the events <see cref="Load"/> pages: the compare tab counts and places
    /// versions over the list, and names the two events it lands on, none of which needs a payload
    /// — and the payloads are what make a boundary heavy. A read that failed says so rather than
    /// answering with an empty history, which would be a different claim.
    /// </remarks>
    public static async Task<BoundaryHistory> History(
        IDcbDbContext context,
        TagQuery boundary,
        Type[]? applies,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return new BoundaryHistory(
                await context.GetEventHeaders(boundary, applies, cancellationToken), Error: null);
        }
        catch (Exception exception)
        {
            return new BoundaryHistory([], exception.Message);
        }
    }

    /// <summary>
    /// The rows a reader's two narrowings leave: the type the log wrote them under, and the text
    /// they carry. The query handed in comes back untouched when neither narrows.
    /// </summary>
    /// <remarks>
    /// Both narrow the same query, so a row has to answer both. The type is matched on the key rather
    /// than on a CLR type, because the key is what the row holds — and a row whose type the uploaded
    /// assemblies no longer describe still has one.
    /// <para>
    /// The payload is matched as it was written, which is the serialized event whole, so the text
    /// looked for reaches the property names as well as the values under them. That is the point of
    /// it: a reader who knows only that an event carried a certain reference should not have to know
    /// which property holds it. Lowered on both sides rather than with a provider's case-insensitive
    /// operator, so this reads the same against SQL Server as it does against Postgres.
    /// </para>
    /// <para>
    /// The payload and nothing else. The position is not asked, unlike on the log's own page, and a
    /// number typed here is text like any other: a history is short enough to run an eye down the
    /// positions of, so a box that also matched them would answer a reference that happens to be
    /// numeric with a row that merely sits at that number.
    /// </para>
    /// </remarks>
    private static IQueryable<DcbEventEntity> Matching(
        IQueryable<DcbEventEntity> rows, string? eventType, string? text)
    {
        var narrowed = string.IsNullOrWhiteSpace(eventType)
            ? rows
            : rows.Where(row => row.EventType == eventType);

        if (string.IsNullOrWhiteSpace(text))
        {
            return narrowed;
        }

        var wanted = text.Trim().ToLower();

        return narrowed.Where(row => row.Data.ToLower().Contains(wanted));
    }

    /// <summary>
    /// Turns one stored row into the event it was written from.
    /// </summary>
    /// <param name="position">The event's global position.</param>
    /// <param name="eventType">The binding key it was stored under, as <c>name:version</c>.</param>
    /// <param name="data">The stored payload.</param>
    /// <param name="written">When it was appended.</param>
    /// <param name="tags">
    /// The tags it was appended under, or null for a streamed event, which has none.
    /// </param>
    /// <remarks>
    /// A row whose type is not registered, or whose payload will not read back, is still listed. The
    /// position, the type and the date are facts of the log itself and hold whatever the payload
    /// turns out to be — and an event the uploaded assemblies no longer describe is worth seeing
    /// rather than quietly dropping from a boundary it is genuinely inside.
    /// <para>
    /// The tags are among those facts, and are kept whatever the payload turns out to be for the
    /// same reason: they are what the log wrote the row under, and a row nothing here can read back
    /// is exactly the one a reader wants the tags of. So is who appended it.
    /// </para>
    /// </remarks>
    /// <param name="writtenBy">Who appended it, or null when nobody is named against the row.</param>
    public static StoredEvent Read(TypeBindingSet bindings, long position, string eventType, string data, DateTimeOffset written,
        IReadOnlyList<string>? tags = null, string? writtenBy = null) =>
        Opened(bindings, position, eventType, data, written, tags ?? []) with { WrittenBy = writtenBy };

    /// <summary>
    /// The row with its payload opened, or with why it would not open: everything about it but who
    /// appended it, which holds whatever became of the payload and is put on afterwards.
    /// </summary>
    private static StoredEvent Opened(TypeBindingSet bindings, long position, string eventType, string data, DateTimeOffset written,
        IReadOnlyList<string> under)
    {
        if (!bindings.EventTypeBindings.TryGetValue(eventType, out var clrType))
        {
            return new StoredEvent(position, eventType, written, data, [],
                $"No uploaded type is registered as {eventType}.", under);
        }

        try
        {
            var @event = DomainSerializer.Current.Deserialize(data, clrType);

            return @event is null
                ? new StoredEvent(position, eventType, written, data, [], "The stored payload is empty.", under)
                : new StoredEvent(position, eventType, written, data, DomainTypeDescriber.ReadState(@event),
                    null, under);
        }
        catch (Exception exception)
        {
            return new StoredEvent(position, eventType, written, data, [], exception.Message, under);
        }
    }
}

/// <summary>One page of the events read out of a boundary.</summary>
/// <param name="Events">Those on this page, in the order asked for.</param>
/// <param name="Total">How many the model applies, across every page.</param>
/// <param name="Page">The page these are, from one.</param>
/// <param name="TotalPages">How many pages there are, never fewer than one.</param>
/// <param name="Error">Why the log could not be read, or null when it was.</param>
public sealed record StoredEvents(
    IReadOnlyList<StoredEvent> Events, int Total, int Page, int TotalPages, string? Error)
{
    /// <summary>
    /// Gets which version of the model each event produced, by position: its place in the whole
    /// history the model applies, counted from one. Empty when the read failed or was not a page
    /// of a model's history.
    /// </summary>
    public IReadOnlyDictionary<long, int> Versions { get; init; } = new Dictionary<long, int>();
}

/// <summary>The shape of a model's whole history, or why it could not be read.</summary>
/// <param name="Rows">The header of every event in the boundary the model applies, in position order; empty when the read failed.</param>
/// <param name="Error">Why the log could not be read, or null when it was.</param>
public sealed record BoundaryHistory(IReadOnlyList<DcbEventHeader> Rows, string? Error);

/// <summary>One event, as the log holds it.</summary>
/// <param name="Position">Its global position.</param>
/// <param name="Type">The binding key it was stored under.</param>
/// <param name="Written">When it was appended.</param>
/// <param name="Data">
/// Its payload, as the log wrote it. Kept beside the properties it was read into, because the Json
/// column shows the row's own text rather than the event serialised again — and shows it whatever
/// became of the read, since a type nothing uploaded describes and a payload that will not open are
/// exactly the rows worth looking at.
/// </param>
/// <param name="State">What its payload holds, or empty when that could not be read.</param>
/// <param name="Error">Why its payload could not be read, or null when it was.</param>
/// <param name="Tags">
/// The tags it was appended under. Empty for a streamed event, which has none to carry; every DCB
/// read, of the log or of a boundary, brings them back with the row — a boundary's read selects
/// through them, and a row may still carry tags beyond the ones it was selected by.
/// </param>
public sealed record StoredEvent(
    long Position,
    string Type,
    DateTimeOffset Written,
    string Data,
    IReadOnlyList<DomainPropertyValue> State,
    string? Error,
    IReadOnlyList<string> Tags)
{
    /// <summary>
    /// Gets who appended it, or null when nobody is named against the row. Audit is a store
    /// concern an application may leave switched off, so a row nobody is named against is an
    /// ordinary row. Every read brings it back: no table draws a column of it, but the page about
    /// one event and the sheet a row opens in over a table both list it.
    /// </summary>
    public string? WrittenBy { get; init; }

    /// <summary>Gets the name half of the key: what the type is written under.</summary>
    public string Name => DomainTypeDescriber.SplitKey(Type).Name;

    /// <summary>
    /// Gets the version half of the key, or null when the key carries none — a row is listed
    /// whatever its type string turns out to be, and one without a version is a name with nothing
    /// to say beside it rather than a row that cannot be drawn.
    /// </summary>
    public string? Version => DomainTypeDescriber.SplitKey(Type).Version;
}
