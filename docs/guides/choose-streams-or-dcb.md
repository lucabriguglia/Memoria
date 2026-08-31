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

```C#
// Streams: pick this order's events out of the customer's stream
public class OrderId(Guid id) : IAggregateId<Order>
{
    public string Id { get; } = id.ToString();
    public IDictionary<string, string>? EventPropertyFilter => new Dictionary<string, string>
    {
        ["OrderId"] = id.ToString()
    };
}

// DCB: the boundary says it
var boundary = TagQuery.AnyOf(new Tag("order", orderId.ToString()));
```

Tags are indexed; the substring match a property filter compiles to on most providers is not.

**A stream id usually becomes one tag.** `customer-42` becomes `customer:42`, and the events that
were in that stream carry that tag. The difference is that they can carry others too.

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
