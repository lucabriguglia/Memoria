using Memoria.EventSourcing.Domain;
using System.Diagnostics;
using Memoria.EventSourcing.Filtering;
using Memoria.EventSourcing.Store.Cosmos.Documents;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;

namespace Memoria.Web.Extensibility;

/// <summary>
/// Answers the pages' two questions from a Cosmos DB store, through the Cosmos SDK.
/// </summary>
/// <param name="client">The account the store lives in.</param>
/// <param name="databaseName">The database the container is in.</param>
/// <param name="containerName">The container the store writes into.</param>
/// <remarks>
/// Not Entity Framework Core. The Cosmos store writes its documents with this SDK and owns their
/// shape — one container discriminated by <c>documentType</c>, partitioned by <c>streamId</c> — so
/// the tool reads them the same way rather than describing that shape a second time in a model.
/// <para>
/// Every read here crosses partitions, because the pages ask across streams rather than about one.
/// That is the cost of the question, not of how it is asked; a store read one stream at a time is
/// what <c>ICosmosDataStore</c> already offers, and it cannot answer "the newest twenty-five of
/// everything".
/// </para>
/// </remarks>
public sealed class CosmosStreamedReads(
    CosmosClient client, string databaseName, string containerName, TypeBindingSet bindings, TotalsCache? totals = null)
    : IStreamedReads
{
    /// <summary>
    /// The total for a narrowing: remembered for a while when there is somewhere to remember it,
    /// since the count is one cross-partition query of its own and the same for every page.
    /// </summary>
    private Task<int> Total(
        string list, Container container, Narrowing narrowing, CancellationToken cancellationToken) =>
        totals is null
            ? Count(container, narrowing, cancellationToken)
            : totals.Total(
                TotalsCache.KeyOf(list, narrowing.Where, narrowing.Values.Select(value => value.Name + "=" + Written(value.Value))),
                () => Count(container, narrowing, cancellationToken));

    private static string Written(object value) =>
        value is IEnumerable<string> items ? string.Join(',', items) : value.ToString() ?? string.Empty;

    /// <summary>
    /// Which order each container was last served in, for the life of the process: a refusal for
    /// want of an index is paid for once, not on every page.
    /// </summary>
    private static readonly OrderingMemory Remembered = new();

    private string Key(params string[] ladder) =>
        string.Join('/', [databaseName, containerName, .. ladder]);

    /// <inheritdoc />
    /// <remarks>
    /// Two queries: one for how many there are and one for the page itself. The count is its own
    /// trip because the page needs a total it cannot infer from twenty-five rows, and Cosmos will
    /// not return both at once.
    /// </remarks>
    public async Task<StoredStreamEvents> Events(
        StreamedEventFilter filter, CancellationToken cancellationToken = default)
    {
        try
        {
            var container = client.GetContainer(databaseName, containerName);

            var narrowing = Narrowing.For(filter);

            var total = await Total("cosmos-events", container, narrowing, cancellationToken);
            var placed = InstanceQuery.Place(filter.Page, total, filter.Size);

            var (documents, notice) =
                await Ordered(container, narrowing, filter, placed, cancellationToken);

            // The same reading the relational log goes through, so a row says the same thing
            // wherever it is met — including one whose type the uploaded assemblies no longer
            // describe, which is listed rather than dropped.
            var read = documents
                .Select(document => new StoredStreamEvent(
                    document.StreamId,
                    document.Id,
                    BoundaryEvents.Read(bindings, document.Sequence, document.EventType, document.Data,
                        document.CreatedDate, writtenBy: document.CreatedBy)))
                .ToList();

            return new StoredStreamEvents(read, total, placed.Page, placed.TotalPages, Error: null)
            {
                OrderingNotice = notice
            };
        }
        catch (Exception exception)
        {
            return new StoredStreamEvents([], Total: 0, Page: 1, TotalPages: 1, exception.Message);
        }
    }

    /// <summary>
    /// One page of events in the fullest order the container can serve.
    /// </summary>
    /// <returns>The documents, and why the order is coarser than asked for when it is.</returns>
    /// <remarks>
    /// <para>
    /// The three keys are what the relational read orders by, and for the same reason: several
    /// events written together share a date exactly, and an order that cannot separate them would
    /// show one row on two pages and another on none.
    /// </para>
    /// <para>
    /// Cosmos will not serve an <c>ORDER BY</c> over more than one property unless a composite index
    /// covers it in that exact direction, and refuses the query outright rather than running it
    /// slowly. The store's own indexing policy ships without such an index deliberately — three were
    /// drafted and cost about 7% on every write while returning nothing to the store's own reads —
    /// so the container this tool is pointed at usually has none.
    /// </para>
    /// <para>
    /// Rather than demand an indexing change to a store this tool only reads, the refusal is taken
    /// as the answer to a question and the date alone is asked for instead, which the store's policy
    /// already indexes. The caller is told, because a coarser order is a real difference rather than
    /// an implementation detail.
    /// </para>
    /// </remarks>
    private async Task<(List<EventDocument> Documents, string? Notice)> Ordered(
        Container container, Narrowing narrowing, StreamedEventFilter filter, PlacedPage placed,
        CancellationToken cancellationToken)
    {
        var direction = filter.Descending ? "DESC" : "ASC";

        var ladder = new (string Order, string? Notice)[]
        {
            ($"c.createdDate {direction}, c.streamId ASC, c.sequence {direction}", null),
            ($"c.createdDate {direction}",
                "This container has no composite index for the full order, so these events are " +
                "ordered by date alone. Events written at the same moment may move between pages.")
        };

        // Climbed from wherever this container was last served rather than from the top each
        // time: the refusal is a fact about the container, and was paid for on the first page.
        var (documents, rung) = await Remembered.Climb(Key("events", direction), ladder.Length,
            rung => Page(container, narrowing, ladder[rung].Order, filter, placed, cancellationToken),
            exception => exception is CosmosException refused && NeedsACompositeIndex(refused));

        return (documents, ladder[rung].Notice);
    }

    /// <summary>
    /// Whether Cosmos refused a query because the order it asks for has no composite index.
    /// </summary>
    /// <remarks>
    /// Read off the message because Cosmos gives no sub-status that separates this from the other
    /// ways a query is malformed, and a bad request that is not this one must still surface as the
    /// error it is rather than be retried in a coarser order.
    /// </remarks>
    private static bool NeedsACompositeIndex(CosmosException exception) =>
        exception.StatusCode is System.Net.HttpStatusCode.BadRequest &&
        exception.Message.Contains("composite index", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether Cosmos refused a query because the property it was asked to order by is not indexed
    /// at all.
    /// </summary>
    /// <remarks>
    /// Not the same as wanting a composite index. That one says the combination is unserved; this
    /// says the property itself was excluded from the container's indexing policy, so no ordering on
    /// it is possible however few keys it has.
    /// </remarks>
    private static bool DoesNotIndex(CosmosException exception) =>
        exception.StatusCode is System.Net.HttpStatusCode.BadRequest &&
        exception.Message.Contains("order-by item is excluded", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// One page of event documents in the given order.
    /// </summary>
    private static Task<List<EventDocument>> Page(
        Container container, Narrowing narrowing, string order, StreamedEventFilter filter,
        PlacedPage placed, CancellationToken cancellationToken) =>
        Read<EventDocument>(container, narrowing
                .Apply(new QueryDefinition(
                    $"SELECT * FROM c WHERE {narrowing.Where} ORDER BY {order} " +
                    "OFFSET @skip LIMIT @take"))
                .WithParameter("@skip", placed.Skip)
                .WithParameter("@take", filter.Size),
            cancellationToken);

    /// <inheritdoc />
    public async Task<EventCount> Count(
        StreamedEventFilter filter, CancellationToken cancellationToken = default)
    {
        try
        {
            var container = client.GetContainer(databaseName, containerName);

            return new EventCount(await Count(container, Narrowing.For(filter), cancellationToken), Error: null);
        }
        catch (Exception exception)
        {
            return new EventCount(null, exception.Message);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// The sequences alone, in sequence order: one small answer the size of the history's key,
    /// rather than the documents themselves. Ordered by a single path, so no composite index is
    /// asked of the container.
    /// </remarks>
    public async Task<EventHistory> History(
        StreamedEventFilter filter, CancellationToken cancellationToken = default)
    {
        try
        {
            var container = client.GetContainer(databaseName, containerName);
            var narrowing = Narrowing.For(filter);
            var positions = await Read<long>(container, narrowing.Apply(new QueryDefinition(
                    $"SELECT VALUE c.sequence FROM c WHERE {narrowing.Where} ORDER BY c.sequence ASC")),
                cancellationToken);

            return new EventHistory(positions, Error: null);
        }
        catch (Exception exception)
        {
            return new EventHistory(null, exception.Message);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// The same ordered query a page is read with, asked for one document at an offset and no
    /// count — so a place past the end is an empty answer rather than a fault.
    /// </remarks>
    public async Task<PlacedStreamEvent> At(
        StreamedEventFilter filter, int index, CancellationToken cancellationToken = default)
    {
        try
        {
            var container = client.GetContainer(databaseName, containerName);

            var (documents, _) = await Ordered(
                container, Narrowing.For(filter), filter with { Size = 1 }, new PlacedPage(1, 1, index), cancellationToken);

            var document = documents.FirstOrDefault();

            return new PlacedStreamEvent(
                document is null
                    ? null
                    : new StoredStreamEvent(
                        document.StreamId,
                        document.Id,
                        BoundaryEvents.Read(bindings, document.Sequence, document.EventType, document.Data,
                            document.CreatedDate, writtenBy: document.CreatedBy)),
                Error: null);
        }
        catch (Exception exception)
        {
            return new PlacedStreamEvent(null, exception.Message);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Aggregates and projections are one container here as they are two tables relationally, told
    /// apart by <c>documentType</c> — and they do not name their type in the same property, so which
    /// kind is being read decides more than a discriminator.
    /// </remarks>
    public async Task<StoredStreamSnapshots> Snapshots(
        StreamedSnapshotFilter filter, CancellationToken cancellationToken = default)
    {
        try
        {
            var container = client.GetContainer(databaseName, containerName);
            var kind = SnapshotKind.Of(filter.Kind);

            var narrowing = Narrowing.ForSnapshots(kind, filter);

            var total = await Total("cosmos-snapshots", container, narrowing, cancellationToken);
            var placed = InstanceQuery.Place(filter.Page, total, filter.Size);

            var (documents, notice) =
                await OrderedSnapshots(container, narrowing, kind, filter, placed, cancellationToken);

            // Nothing is deserialized: both pages list what the store says about a snapshot rather
            // than what the snapshot holds, which is the same reading the relational rows go through.
            var read = documents
                .Select(document => new StoredStreamSnapshot(
                    document.StreamId,
                    document.Id,
                    document.Type,
                    document.Version,
                    document.LatestEventSequence,
                    document.CreatedDate,
                    document.UpdatedDate))
                .ToList();

            return new StoredStreamSnapshots(read, total, placed.Page, placed.TotalPages, Error: null)
            {
                OrderingNotice = notice
            };
        }
        catch (Exception exception)
        {
            return new StoredStreamSnapshots([], Total: 0, Page: 1, TotalPages: 1, exception.Message);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// One query and one partition, unlike everything else here: an address names the stream, which
    /// is what the container is partitioned by, so this is the one read that knows where to look.
    /// No ordering and no count either — an address reaches one document or none.
    /// </remarks>
    public async Task<ReadStreamModel> Model(
        StreamedModelAddress address, CancellationToken cancellationToken = default)
    {
        try
        {
            var container = client.GetContainer(databaseName, containerName);
            var kind = SnapshotKind.Of(address.Kind);

            var query = new QueryDefinition(
                    $"SELECT c.streamId, c.id, {kind.TypeProperty} AS type, c.version, " +
                    "c.latestEventSequence, c.data, c.createdDate, c.createdBy, " +
                    "c.updatedDate, c.updatedBy " +
                    "FROM c WHERE c.documentType = @documentType AND c.streamId = @streamId " +
                    "AND c.id = @id")
                .WithParameter("@documentType", kind.DocumentType)
                .WithParameter("@streamId", address.StreamId)
                .WithParameter("@id", address.StoreId);

            var documents = await Read<ModelDocument>(container, query, cancellationToken);

            var document = documents.FirstOrDefault();

            return new ReadStreamModel(
                document is null
                    ? null
                    : new StoredStreamModel(
                        document.StreamId,
                        document.Id,
                        document.Type,
                        document.Version,
                        document.LatestEventSequence,
                        document.Data,
                        document.CreatedDate,
                        document.CreatedBy,
                        document.UpdatedDate,
                        document.UpdatedBy),
                Error: null);
        }
        catch (Exception exception)
        {
            return new ReadStreamModel(Snapshot: null, exception.Message);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// One query and one partition, like <see cref="Model"/>: an address names the stream, which is
    /// what the container is partitioned by. The discriminator is still asked for, because the
    /// three document types share a partition and an id built from different things, so a key of
    /// the right shape can land on a snapshot — see the collision the store itself detects.
    /// </remarks>
    public async Task<ReadStreamEvent> Event(
        StreamedEventAddress address, CancellationToken cancellationToken = default)
    {
        try
        {
            var container = client.GetContainer(databaseName, containerName);

            var query = new QueryDefinition(
                    "SELECT * FROM c WHERE c.documentType = @documentType " +
                    "AND c.streamId = @streamId AND c.id = @id")
                .WithParameter("@documentType", DocumentType.Event)
                .WithParameter("@streamId", address.StreamId)
                .WithParameter("@id", address.Id);

            var documents = await Read<EventDocument>(container, query, cancellationToken);

            var document = documents.FirstOrDefault();

            return new ReadStreamEvent(
                document is null
                    ? null
                    : new StoredStreamEvent(
                        document.StreamId,
                        document.Id,
                        BoundaryEvents.Read(bindings, document.Sequence, document.EventType, document.Data,
                            document.CreatedDate, writtenBy: document.CreatedBy)),
                Error: null);
        }
        catch (Exception exception)
        {
            return new ReadStreamEvent(Event: null, exception.Message);
        }
    }

    /// <summary>
    /// One snapshot read whole: what <see cref="SnapshotDocument"/> carries, and the payload and
    /// audit properties a page about a single model shows besides.
    /// </summary>
    private sealed class ModelDocument : SnapshotDocument
    {
        [JsonProperty("data")] public string Data { get; set; } = string.Empty;

        [JsonProperty("createdBy")] public string? CreatedBy { get; set; }

        [JsonProperty("updatedBy")] public string? UpdatedBy { get; set; }
    }

    /// <summary>
    /// One page of snapshots in the fullest order the container can serve.
    /// </summary>
    /// <remarks>
    /// The same bargain the log is read under: ask for the three keys the relational page orders by,
    /// and take a refusal for want of a composite index as the answer to a question rather than an
    /// error. Four orders here rather than two, because either date can be sorted either way.
    /// </remarks>
    private async Task<(List<SnapshotDocument> Documents, string? Notice)> OrderedSnapshots(
        Container container, Narrowing narrowing, SnapshotKind kind, StreamedSnapshotFilter filter,
        PlacedPage placed, CancellationToken cancellationToken)
    {
        const string created = "c.createdDate";
        const string updated = "c.updatedDate";

        var asked = filter.Sort is InstanceSort.Created ? created : updated;
        var direction = filter.Descending ? "DESC" : "ASC";

        // Each is asked for in turn until the container serves one, because a refusal can come at
        // either step: dropping the tie-breakers answers a missing composite index, but not a date
        // the container does not index at all, and the coarser order meets that refusal too.
        //
        // The store's own indexing policy indexes the date a document was created and not the date
        // it was last written, because nothing the store itself reads sorts on the latter. On such a
        // container the updated order is impossible rather than merely unindexed, so the other date
        // is asked for and the reader is told which one they are looking at — showing one order
        // under the other column's heading would be a lie the page could not be talked out of.
        var ladder = new List<(string Order, string? Notice)>
        {
            ($"{asked} {direction}, c.streamId ASC, c.id ASC", null),
            ($"{asked} {direction}",
                "This container has no composite index for the full order, so these models are " +
                "ordered by date alone. Models written at the same moment may move between pages.")
        };

        if (asked == updated)
        {
            ladder.Add(($"{created} {direction}",
                "This container does not index the date these models were last written, so they " +
                "are ordered by when they were first written instead."));
        }

        // Climbed from wherever this container was last served for this kind of model, this date
        // and this direction: each is its own ladder, since a refusal at one rung says nothing
        // about a ladder that asks for a different property.
        var (documents, rung) = await Remembered.Climb(
            Key(kind.DocumentType, asked, direction), ladder.Count,
            rung => SnapshotPage(container, narrowing, kind, ladder[rung].Order, filter, placed, cancellationToken),
            exception => exception is CosmosException refused &&
                         (NeedsACompositeIndex(refused) || DoesNotIndex(refused)));

        return (documents, ladder[rung].Notice);
    }

    /// <summary>
    /// One page of snapshot documents in the given order.
    /// </summary>
    /// <remarks>
    /// The type is projected under one name whichever kind was asked for, so the row that comes back
    /// has the same shape for both and nothing downstream needs to know which it read.
    /// </remarks>
    private static Task<List<SnapshotDocument>> SnapshotPage(
        Container container, Narrowing narrowing, SnapshotKind kind, string order,
        StreamedSnapshotFilter filter, PlacedPage placed, CancellationToken cancellationToken) =>
        Read<SnapshotDocument>(container, narrowing
                .Apply(new QueryDefinition(
                    $"SELECT c.streamId, c.id, {kind.TypeProperty} AS type, c.version, " +
                    $"c.latestEventSequence, c.createdDate, c.updatedDate " +
                    $"FROM c WHERE {narrowing.Where} ORDER BY {order} OFFSET @skip LIMIT @take"))
                .WithParameter("@skip", placed.Skip)
                .WithParameter("@take", filter.Size),
            cancellationToken);

    /// <summary>
    /// Which of the two stored models is being read, and how the container names it.
    /// </summary>
    /// <param name="DocumentType">The discriminator its documents carry.</param>
    /// <param name="TypeProperty">The property holding the binding key of its type.</param>
    private sealed record SnapshotKind(string DocumentType, string TypeProperty)
    {
        // Fully qualified: this record's own DocumentType property shadows the class of that name
        // inside its body, so the unqualified reference resolves to the wrong thing.
        public static SnapshotKind Of(StreamedModelKind kind) =>
            kind is StreamedModelKind.Projection
                ? new SnapshotKind(
                    Memoria.EventSourcing.Store.Cosmos.Documents.DocumentType.Projection,
                    "c.projectionType")
                : new SnapshotKind(
                    Memoria.EventSourcing.Store.Cosmos.Documents.DocumentType.Aggregate,
                    "c.aggregateType");
    }

    /// <summary>
    /// One snapshot as both kinds are read into: the store's account of a model, with its type under
    /// one name whichever property the document held it in.
    /// </summary>
    private class SnapshotDocument
    {
        [JsonProperty("streamId")] public string StreamId { get; set; } = string.Empty;

        [JsonProperty("id")] public string Id { get; set; } = string.Empty;

        [JsonProperty("type")] public string Type { get; set; } = string.Empty;

        [JsonProperty("version")] public int Version { get; set; }

        [JsonProperty("latestEventSequence")] public int LatestEventSequence { get; set; }

        [JsonProperty("createdDate")] public DateTimeOffset CreatedDate { get; set; }

        [JsonProperty("updatedDate")] public DateTimeOffset UpdatedDate { get; set; }
    }

    /// <summary>
    /// How many documents the narrowing leaves, across every page of them.
    /// </summary>
    private static async Task<int> Count(
        Container container, Narrowing narrowing, CancellationToken cancellationToken)
    {
        var query = narrowing.Apply(
            new QueryDefinition($"SELECT VALUE COUNT(1) FROM c WHERE {narrowing.Where}"));

        var counted = await Read<int>(container, query, cancellationToken);

        return counted.FirstOrDefault();
    }

    /// <summary>
    /// What a page of the log has been narrowed to, as a condition and the values it holds.
    /// </summary>
    /// <param name="Where">The condition, with a parameter wherever a value goes.</param>
    /// <param name="Values">Those values, by parameter name.</param>
    /// <remarks>
    /// One narrowing serves both the count and the page, which is what makes the total the total of
    /// what was asked for rather than of the whole log. Every value is a parameter: a stream pattern
    /// and a line of typed text both arrive from the address bar, and neither is going anywhere near
    /// the text of a query.
    /// </remarks>
    private sealed record Narrowing(string Where, IReadOnlyList<(string Name, object Value)> Values)
    {
        /// <summary>
        /// The narrowing one page of the log was asked for.
        /// </summary>
        /// <remarks>
        /// The same three the relational read applies, in the same meaning.
        /// <para>
        /// The stream is matched with <c>LIKE</c> because its pattern is one:
        /// <see cref="IdShape"/> writes the stream's own id with a wildcard where each value it was
        /// built from stood, and the values are not always at the end of it.
        /// </para>
        /// <para>
        /// The typed text is matched with <c>CONTAINS</c> rather than <c>LIKE</c>, and that is the
        /// difference between the two: this text was typed by someone looking for it, so <c>%</c>
        /// and <c>_</c> are characters they may well be looking for rather than wildcards. Its third
        /// argument asks Cosmos to ignore case, which is what the relational read lowers both sides
        /// to achieve.
        /// </para>
        /// </remarks>
        public static Narrowing For(StreamedEventFilter filter)
        {
            var conditions = new List<string> { "c.documentType = @documentType" };
            var values = new List<(string, object)> { ("@documentType", DocumentType.Event) };

            if (!string.IsNullOrWhiteSpace(filter.EventType))
            {
                conditions.Add("c.eventType = @eventType");
                values.Add(("@eventType", filter.EventType));
            }

            if (filter.EventTypes is { Count: > 0 } applied)
            {
                // The set a model folds, as one parameter rather than a clause per key: what is
                // asked is whether the row's own key is among them, which is the one question.
                conditions.Add("ARRAY_CONTAINS(@eventTypes, c.eventType)");
                values.Add(("@eventTypes", applied.ToArray()));
            }

            if (filter.Properties is { Count: > 0 })
            {
                // The needles the store's own fold looks for, matched against the payload as text.
                // CONTAINS with no third argument, unlike the typed text above: this is JSON the
                // store wrote rather than something someone typed, so its case is the case it has.
                var needles = filter.Properties
                    .Select((property, index) => (
                        Name: $"@property{index}",
                        Value: $"{JsonConvert.ToString(property.Key)}:" +
                               $"{EventPropertyFilterValue.ToJsonLiteral(property.Value)}"))
                    .ToList();

                foreach (var needle in needles)
                {
                    conditions.Add($"CONTAINS(c.data, {needle.Name})");
                    values.Add((needle.Name, needle.Value));
                }
            }

            if (!string.IsNullOrWhiteSpace(filter.StreamPattern))
            {
                conditions.Add("c.streamId LIKE @streamPattern");
                values.Add(("@streamPattern", filter.StreamPattern));
            }

            if (filter.BeforeSequence is { } bound)
            {
                // Below rather than at or below, as the relational read has it: what is asked is
                // which event a row follows, and the row itself is not the answer.
                conditions.Add("c.sequence < @beforeSequence");
                values.Add(("@beforeSequence", bound));
            }

            if (!string.IsNullOrWhiteSpace(filter.Text))
            {
                // The row's own key as well as the stream it names and the payload it carries — the
                // same three the relational read looks in, and for the reason written there: how the
                // store builds a key is its business, so the stream is looked in separately.
                conditions.Add(
                    "(CONTAINS(c.streamId, @text, true) OR CONTAINS(c.id, @text, true) " +
                    "OR CONTAINS(c.data, @text, true))");

                values.Add(("@text", filter.Text.Trim()));
            }

            return new Narrowing(string.Join(" AND ", conditions), values);
        }

        /// <summary>
        /// The narrowing one page of stored models was asked for.
        /// </summary>
        /// <remarks>
        /// The kind is the one narrowing always applied, and it is what keeps an aggregate off the
        /// projections page: one container holds both, and only the discriminator tells them apart.
        /// <para>
        /// Two of the four differ from the log's. The model type is read from whichever property
        /// this kind names it in, and the identifier pattern is held against the whole stored key —
        /// the store joins the id and the type's version with a colon, so a pattern matched against
        /// the id alone would find nothing whenever the id ends in something fixed.
        /// </para>
        /// <para>
        /// The typed text looks in the stream and the stored key and deliberately not in the
        /// payload, which is where it parts company with the log. Neither page shows a stored
        /// model's state, so a row matching on it would come back with nothing on it carrying what
        /// was typed.
        /// </para>
        /// </remarks>
        public static Narrowing ForSnapshots(SnapshotKind kind, StreamedSnapshotFilter filter)
        {
            var conditions = new List<string> { "c.documentType = @documentType" };
            var values = new List<(string, object)> { ("@documentType", kind.DocumentType) };

            if (!string.IsNullOrWhiteSpace(filter.ModelType))
            {
                conditions.Add($"{kind.TypeProperty} = @modelType");
                values.Add(("@modelType", filter.ModelType));
            }

            if (!string.IsNullOrWhiteSpace(filter.StreamPattern))
            {
                conditions.Add("c.streamId LIKE @streamPattern");
                values.Add(("@streamPattern", filter.StreamPattern));
            }

            if (!string.IsNullOrWhiteSpace(filter.IdentifierPattern))
            {
                conditions.Add("c.id LIKE @identifierPattern");
                values.Add(("@identifierPattern", $"{filter.IdentifierPattern}:%"));
            }

            if (!string.IsNullOrWhiteSpace(filter.Text))
            {
                conditions.Add(
                    "(CONTAINS(c.streamId, @text, true) OR CONTAINS(c.id, @text, true))");

                values.Add(("@text", filter.Text.Trim()));
            }

            return new Narrowing(string.Join(" AND ", conditions), values);
        }

        /// <summary>
        /// Puts this narrowing's values on a query written against its condition.
        /// </summary>
        public QueryDefinition Apply(QueryDefinition query) =>
            Values.Aggregate(query, (carried, value) => carried.WithParameter(value.Name, value.Value));
    }

    /// <summary>
    /// Drains one query across every partition it reaches.
    /// </summary>
    private static async Task<List<T>> Read<T>(
        Container container, QueryDefinition query, CancellationToken cancellationToken)
    {
        var results = new List<T>();

        using var iterator = container.GetItemQueryIterator<T>(query);

        while (iterator.HasMoreResults)
        {
            results.AddRange(await iterator.ReadNextAsync(cancellationToken));
        }

        return results;
    }
}
