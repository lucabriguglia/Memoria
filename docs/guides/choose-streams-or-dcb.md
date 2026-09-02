# Streams or DCB?

Memoria ships two consistency models. They are independent packages, they share no tables, and an
application can use both. This is how to pick.

## The short answer

**Stay with streams.** They are simpler, they have three store providers to DCB's one, and most
domains have decisions whose boundary really is one aggregate.

**Reach for DCB when a decision's boundary is not the shape of any stream you can draw** — when it
spans two entities, or differs per decision.

## The test

Write down what one decision must be consistent with, then try to name a stream containing exactly
that and no more.

If you can, use streams. If every candidate stream is either **missing facts the decision needs** or
**dragging in decisions that have nothing to do with it**, that is the DCB case.

The worked example is a student subscribing to a course, where the rule spans a course's capacity and
a student's course count. A stream per course cannot see the student's other subscriptions; a stream
per student cannot see how full the course is; one stream for the school serialises every
subscription in it. See
[Dynamic consistency boundaries](../concepts/dynamic-consistency-boundaries.md).

## Side by side

| | Streams | DCB |
|---|---|---|
| Boundary | A stream, chosen at design time | A tag query, chosen per decision |
| Concurrency | `expectedEventSequence` against one stream | `AppendCondition` against a boundary |
| Ordering | A sequence within each stream | One position across the whole log |
| Contention | Two writes to one stream | Two writes whose boundaries overlap |
| Stores | Entity Framework Core, Cosmos DB, + Identity, + InMemory | Entity Framework Core only |
| Selecting an aggregate's events | `IStreamId`, plus `eventPropertyFilter` for shared streams | Tags |
| Failure on conflict | `memoria/concurrency-conflict` | `memoria/concurrency-conflict` — the same |

## What does not change

Events, `[EventType]` versioning, the fold, `ReadMode`, snapshots, `Result`/`Failure`, the
replaceable serializer, and the store failure classifications are shared. A retry policy keyed on
`memoria/concurrency-conflict` works against either model without modification.

An `IEvent` type can be appended by both. Event bindings are shared for that reason; aggregate and
projection bindings are not, so a streamed `Order` and a DCB `Order` can coexist and neither has to
be renamed.

## Migrating a concept from streams to DCB

**`eventPropertyFilter` becomes a tag.** It exists so several aggregates can share a stream, matching
on a property inside the payload. DCB has no equivalent and does not need one — put the value in a
tag and query it directly:

Both live on the identifier, so the two models line up almost exactly:

```C#
// Streams: the stream is passed in, and the id narrows to this aggregate's events inside it
public class OrderId(Guid id) : IAggregateId<Order>
{
    public string Id { get; } = id.ToString();
    public IDictionary<string, string>? EventPropertyFilter => new Dictionary<string, string>
    {
        ["OrderId"] = id.ToString()
    };
}

// DCB: the id carries the whole boundary, because tags select on their own
public class OrderId(Guid id) : IDcbAggregateId<Order>
{
    public string Id { get; } = id.ToString();
    public TagQuery Boundary { get; } = TagQuery.AnyOf(new Tag("order", id.ToString()));
}
```

The difference is that a stream still has to be passed alongside the streamed id, while a DCB
identifier is self-contained — `dcb.GetAggregate(new OrderId(id))` needs nothing else. Tags are also
indexed, where the substring match a property filter compiles to on most providers is not.

**A stream id usually becomes one tag.** `customer-42` becomes `customer:42`, and the events that
were in that stream carry that tag. The difference is that they can carry others too.

## What it costs

Measured with [the benchmarks in this repo](https://github.com/lucabriguglia/Memoria/tree/main/benchmarks),
on in-memory SQLite and on SQL Server 2022 in a container, with the same event type, the same model
state and the same fold on both sides — so what is measured is the store.

**Reads are a wash.** DCB issues the same number of database round trips as streams. On SQLite the
timings converge as the set grows — 1.34× at ten events, 1.01× at a thousand, as the tag semi-join's
small fixed cost amortises away. On SQL Server the ratios scatter between 0.82× and 1.23× with one
1.82× outlier at ten events, which is to say the difference is below the noise of a containerised
engine: DCB is faster in about as many cases as it is slower.

A stream is a range on one indexed column; a boundary is a semi-join against `DcbEventTags` over the
same rows. A snapshot read is one row either way, and stays flat however long the history gets —
which is the point of snapshots in both models.

**Appends cost about twice as much**, on both engines, allocating 2.7× as much, flat in the number of
events already stored:

| `SaveAggregate`, median | Streams | DCB     | Ratio |
|-------------------------|---------|---------|-------|
| SQLite, in memory       | ~0.95 ms | ~2.1 ms | ~2.1× |
| SQL Server, container    | ~7.4 ms  | ~14.5 ms | ~2.0× |

That the ratio survives the move is worth saying plainly, because we expected it not to. A streamed
append is 2 database commands on SQL Server and a DCB append is 7 — it claims the tag head rows,
reads the boundary's position, writes the events, replaces the tokens, writes the tags, then the
snapshot. The prediction was that five extra round trips would widen the gap sharply once each one
crossed a network. It did not: the absolute cost rose about sevenfold on both sides and the ratio
stayed put, because a streamed append pays transaction and commit costs that scale the same way.

The caveat that remains is distance. The container is on the same machine, so a round trip is
loopback rather than a network hop. The further the database, the more those five extra commands
cost, and the ratio is the first thing that will move. If your database is remote, count the round
trips rather than trusting this table.

None of this is a reason to pick one or the other. A decision whose boundary is not the shape of any
stream cannot be made correctly under streams at any price, and an append that a stream can express
is not worth five extra round trips. Pick on the boundary, as above; use these numbers to size the
consequence.

## Known limits

- **`TagQuery` is `AnyOf` only.** A boundary is a disjunction of tags. Conjunction is not in 1.8.0.
- **Do not build a catch-up subscription on `Position` yet.** It is monotonic but not gap-free — see
  [Entity Framework Core (DCB)](../reference/configuration/dcb-ef-core.md#positions-are-not-gap-free).
- **One row per distinct tag, forever**, in `DcbTagHeads`.
- **No Cosmos DB provider**, for a structural reason rather than an effort one — see
  [Providers](../concepts/providers.md).

## Using both

Nothing stops you. Register each, and give each its own `DbContext` or apply both sets of entity
configurations to one:

```C#
services.AddMemoriaEventSourcing(typeof(Program));
services.AddMemoriaEntityFrameworkCore<MyStoreDbContext>();

services.AddMemoriaDcb(typeof(Program));
services.AddMemoriaDcbEntityFrameworkCore<MySchoolDbContext>();
```

Registration is additive and order-independent. The two stores never read each other's tables.

## Related

- [Dynamic consistency boundaries](../concepts/dynamic-consistency-boundaries.md)
- [Aggregates and streams](../concepts/aggregates-and-streams.md)
- [Multiple aggregates per stream](multiple-aggregates-per-stream.md) — the streamed answer to a
  shared boundary, and its limits
