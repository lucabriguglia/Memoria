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

A [`TagQuery`](../reference/configuration/dcb-ef-core.md) is a boundary. The commonest shape is the
events carrying any of its tags:

```C#
var boundary = TagQuery.AnyOf(new Tag("course", "maths"), new Tag("student", "alice"));
```

That one event above falls inside every boundary naming `course:maths` *and* every boundary naming
`student:alice`. Seen through the course it fills a seat; seen through the student it uses up one of
their ten. One event, two meanings, no duplication.

## Union and intersection boundaries

`AnyOf` is a **union**: the events carrying *any* of its tags. `AllOf` is an **intersection**: only
the events carrying *all* of them.

```C#
// Everything about the course, plus everything about the student.
var union = TagQuery.AnyOf(new Tag("course", "maths"), new Tag("student", "alice"));

// Only the events concerning both.
var intersection = TagQuery.AllOf(new Tag("course", "maths"), new Tag("student", "alice"));
```

| Boundary | Selects | Use for |
|---|---|---|
| `AnyOf(a, b)` | events carrying `a` **or** `b` | a rule spanning both things — the course's capacity *and* the student's ten |
| `AllOf(a, b)` | events carrying `a` **and** `b` | a fact about the pair — is alice already on maths? |

The subscription rule above needs the union: a boundary of only the events concerning both cannot see
how full the course is. But *"is alice already on maths?"* is answered by a single event, and reading
it through the union means folding every seat in the course and every course alice has ever taken to
find it. The intersection reads that one event, and keeps doing so as the school grows.

Narrowing the fold also removes work from the model. A model over the union has to sort out what it
folded — is this subscription about *my* course, or another of this student's? — so its `Apply` is
full of `when subscribed.CourseId == CourseId` guards. Under an intersection the boundary has already
done that, and the guards go away.

Two things to be careful of:

- **An intersection narrows what you read, not what you may condition on.** A decision that folded
  `AnyOf(course, student)` must condition its append on `AnyOf(course, student)`. Conditioning on the
  narrower `AllOf` would accept an append resting on a capacity that has since changed. Condition on
  the boundary you folded, or a wider one.
- **An intersection is only as good as the tagging.** `CourseDefinedEvent` is appended under the
  course alone, so it is not inside `AllOf(course:maths, student:alice)` however much it concerns the
  course that alice is on. If a model needs it, its boundary is a union.

A boundary that mixes the two — an *or* of *and*s — is not in this release. Neither is a
per-boundary filter on event type: `EventTypeFilter` on the model does that job.

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

**A read model differs from a write model in one thing only: it never produces events.** Everything
else is the same and is offered on both — identity, `LatestPosition`, the boundary in `Tags`, all
four read modes, `GetInMemory…`, `Get…`, `Update…`, and the same `Aggregate Folded` diagnostics. Only
`Add`, `UncommittedEvents` and `SaveAggregate`'s append belong to the write model.

```
IEventSourcedModel        Version, EventTypeFilter, Apply, IsEventHandled
 ├ IStreamedModel        + StreamId, LatestEventSequence   (int, within one stream)
 │   ├ IAggregateRoot    + AggregateId, UncommittedEvents
 │   └ IProjection       + ProjectionId
 └ IDcbModel             + LatestPosition                  (long, across the whole log)
     ├ IDcbAggregateRoot + AggregateId, Tags, UncommittedEvents
     └ IDcbProjection    + ProjectionId
```

## Identifiers carry their boundary

`IDcbAggregateId` and `IDcbProjectionId` expose a `TagQuery Boundary` alongside `Id`. It is the DCB
answer to `IAggregateId.EventPropertyFilter` — how this model's events are selected — except that
tags select on their own, so it is the whole boundary rather than a narrowing inside a stream:

```C#
public class SubscriptionDecisionId(string courseId, string studentId)
    : IDcbAggregateId<SubscriptionDecision>
{
    public string Id { get; } = $"{courseId}-{studentId}";

    public TagQuery Boundary { get; } =
        TagQuery.AnyOf(new Tag("course", courseId), new Tag("student", studentId));
}

// Everything aggregate-scoped then needs only the identifier
var decision = await dcb.GetAggregate(new SubscriptionDecisionId("maths", "alice"));
```

Binding them makes it impossible for a model and the events it may read to disagree. It does not fix
a boundary at design time the way a stream does — the identifier is constructed per decision, so its
boundary varies with it.

The boundary must be stable for a given `Id`. A snapshot is keyed by the boundary that produced it,
so an identifier whose boundary changes — because you edited it and redeployed — misses its own
snapshots and rebuilds them. Wasteful, never wrong.

The event-level methods (`GetEvents`, `GetLatestPosition`, `SaveEvents`) still take a `TagQuery`
directly. They are not aggregate-scoped, so there is no identifier to carry one.

Once the model has been loaded, its own `Tags` are set from that boundary. A model spanning more than
one entity can read them in `Apply` to know which ones it is about, and `Add` without explicit tags
appends under the boundary the model was folded from.

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
