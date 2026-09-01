# Dynamic consistency boundaries

A consistency boundary is the set of events a decision must be consistent with. In the streamed
model that set is a **stream**, chosen when you design the aggregate. Under **dynamic consistency
boundaries** (DCB) it is a **query over tags**, chosen per decision and evaluated at the moment of
the write.

Everything else is the same. Events are the same events, folding is the same fold, `Result` and
`ReadMode` and the failure classifications are the same. Only what defines "consistent with what"
changes.

## The problem streams cannot solve

A school where a student may take at most ten courses, and a course has a fixed capacity. Subscribing
has to check both:

- the course exists and is not full — a fact about the **course**
- the student is registered and not already on it — a fact about **both**
- the student is on fewer than ten courses — a fact about the **student**

Pick any stream:

| Stream | What it cannot see |
|---|---|
| One per course | The student's other subscriptions |
| One per student | How full the course is |
| One for the school | Nothing — but now every subscription in the school is serialised |

There is no right answer, because the decision's boundary is not the same shape as any stream. It is
*this course and this student*, and a different subscription has a different one.

## Tags and boundaries

An event carries **tags** naming the things it concerns:

```C#
Add(new StudentSubscribedEvent(studentId, courseId),
    new Tag("course", courseId), new Tag("student", studentId));
```

A [`TagQuery`](../reference/configuration/dcb-ef-core.md) is a boundary — the events carrying any of
its tags:

```C#
var boundary = TagQuery.AnyOf(new Tag("course", "maths"), new Tag("student", "alice"));
```

That one event above falls inside every boundary naming `course:maths` *and* every boundary naming
`student:alice`. Seen through the course it fills a seat; seen through the student it uses up one of
their ten. One event, two meanings, no duplication.

> **1.8.0 supports `AnyOf` only.** A boundary is a disjunction: any of these tags. Conjunction —
> events carrying *both* `course:maths` and `student:alice` — is deliberately absent, because it
> needs a different query shape and changes which rows an append must lock. `TagQuery` is shaped so
> it can be added without a breaking change.

## The append condition

Positions are global to the log, not per stream. A decision reads where its boundary stands, decides,
and then appends **on condition that the boundary has not moved**:

```C#
var position = (await dcb.GetLatestPosition(boundary)).Value;

// ... fold the boundary, decide ...

var result = await dcb.SaveEvents(events, new AppendCondition(boundary, position));
```

If anything matching the boundary was appended in between, the decision rested on stale facts and the
append is refused with `memoria/concurrency-conflict` — the same failure a stream conflict produces,
so an existing retry policy works unchanged. The failure carries `latestPosition`, so a retry needs
no extra read.

Two subscriptions contend **only** when their boundaries overlap. `dave/maths` and `erin/greek` share
neither a course nor a student, so both commit; `frank/greek` and `gina/greek` share a course, so one
of them loses.

Pass no condition to append unconditionally. That is correct only when the decision depended on
nothing it read — registering a student, defining a course. An unconditional append still makes
conditioned appends over the same tags fail; it simply has nothing of its own to be invalidated.

## Reading the boundary

Read the position **before** the fold, and never after:

```C#
var position = await dcb.GetLatestPosition(boundary);
var events = await dcb.GetEvents(boundary, model.EventTypeFilter);
```

The order is not cosmetic. The position is a claim about what the decision saw, and an event landing
between the two reads breaks that claim in one of two directions:

| Order | An event lands in between | Outcome |
|---|---|---|
| Position, then fold | The fold sees more than the position admits | The append is **refused**; retry |
| Fold, then position | The position admits more than the fold saw | The append is **accepted** on a decision that never read it |

The second is a lost update, signed off by the very check meant to prevent it. Reading the position
first is what makes the failure land on the safe side.

The position cannot simply come from the fold instead. The fold stops at the last event the model's
`EventTypeFilter` accepted, which can be behind the boundary's head with nothing else running at all
— so conditioning on it would refuse every append that happened to follow an event the model ignores.

There is no one-call form of this cycle, and that is deliberate: the position, the fold and the
append belong to the decision, and a helper that hid them would hide the ordering above with them.
`UpdateAggregate` is *not* it — like its streamed counterpart it only refreshes a snapshot and
appends nothing.

Note also that `GetInMemoryAggregate` constructs the model itself, so a decision model that must know
what it is about before it folds — one spanning two entities, say — reads with `GetEvents` and folds
by hand instead.

## Models

`DcbAggregateRoot` and `DcbProjection` reuse the fold from `EventSourcedModel` unchanged — version
tracking, `EventTypeFilter`, `Apply`. They differ from `AggregateRoot` and `Projection` in exactly
two ways: they belong to no stream, and they record how far they folded as a `long LatestPosition`
across the whole log rather than an `int` sequence within one stream.

```
IEventSourcedModel        Version, EventTypeFilter, Apply, IsEventHandled
 ├ IStreamedModel        + StreamId, LatestEventSequence   (int, within one stream)
 │   ├ IAggregateRoot    + AggregateId, UncommittedEvents
 │   └ IProjection       + ProjectionId
 └ IDcbModel             + LatestPosition                  (long, across the whole log)
     ├ IDcbAggregateRoot + AggregateId, Tags, UncommittedEvents
     └ IDcbProjection    + ProjectionId
```

## Snapshots

A snapshot is keyed by the model **and the boundary that produced it**. The same aggregate id folded
over a wider boundary is a different state, so reading it back under a different query misses rather
than returning the wrong fold. All four [read modes](read-modes.md) behave as they do for streams.

## What it costs

- **A row per distinct tag, forever.** `DcbTagHeads` holds one row per tag ever written under or
  conditioned on — one per student, per course, per order. Small, and the price of contention that
  follows the boundary instead of the whole store.
- **Tags are case-sensitive**, and the store pins a case-sensitive collation to keep the database
  agreeing with .NET. See [Entity Framework Core (DCB)](../reference/configuration/dcb-ef-core.md).
- **Relational only.** There is no Cosmos DB provider, for a reason that is not effort — see
  [Providers](providers.md).

## Related

- [Streams or DCB?](../guides/choose-streams-or-dcb.md) — choosing between the two
- [Entity Framework Core (DCB)](../reference/configuration/dcb-ef-core.md) — configuration and schema
- [Aggregates and streams](aggregates-and-streams.md) — the streamed model
- [Upgrade to 1.8.0](../guides/upgrade-1.8.0.md)
