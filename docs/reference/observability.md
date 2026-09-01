# Observability

Memoria's event stores write to the current `System.Diagnostics.Activity` as they work. Nothing is
configured, and nothing is emitted unless something is listening — if `Activity.Current` is `null`,
no tags are built and no allocation happens on the write path.

The events answer one question in particular: **this aggregate is in a state nobody expects — which
events produced it?** [Aggregate Folded](#aggregate-folded) records that at the moment the fold
happened, so it stays true however the aggregate's type and event filter change later.

## Collecting the events

`Activity` is the .NET primitive behind OpenTelemetry, so an OpenTelemetry tracing pipeline picks
these up with no Memoria-specific configuration:

```csharp
services.AddOpenTelemetry().WithTracing(tracing => tracing
    .AddAspNetCoreInstrumentation()
    .AddOtlpExporter());
```

Memoria adds events to whatever activity is current. It does not start activities of its own, so
there must be an ambient activity — from ASP.NET Core instrumentation, or one you start yourself:

```csharp
using var activity = myActivitySource.StartActivity("PlaceOrder");
await domainService.SaveAggregate(streamId, aggregateId, order, expectedEventSequence);
```

To read them directly, in a test or a diagnostic endpoint:

```csharp
var folds = Activity.Current!.Events
    .Where(activityEvent => activityEvent.Name == AggregateDiagnostics.AggregateFoldedEventName);
```

## Event catalogue

### Aggregate Folded

Emitted by every store whenever it folds events into an aggregate and writes the resulting snapshot.

| Tag | Type | Meaning |
|---|---|---|
| `streamId` | string | The stream the events were read from |
| `aggregateId` | string | Aggregate store id, `{IAggregateId.Id}:{[AggregateType] version}` |
| `appliedFromSequence` | int | Sequence of the first event folded |
| `appliedToSequence` | int | Sequence of the last event folded |
| `appliedCount` | int | How many events were folded |
| `versionBefore` | int | The aggregate's version before the fold |
| `versionAfter` | int | The aggregate's version after the fold |

`appliedCount` counts events the fold **consumed**. `versionAfter - versionBefore` counts those that
actually **changed** the aggregate. They differ when an event matches the aggregate's
`EventTypeFilter` but its `Apply` returns `false` — the event was read and ignored. That gap is
usually the interesting part of the answer.

Emitted from:

| Store | Operations |
|---|---|
| Entity Framework Core | `GetAggregate` (cold build), `UpdateAggregate`, `SaveAggregate`, `TrackEventEntities` |
| Cosmos DB | `GetAggregate` (cold build), `UpdateAggregate`, `SaveAggregate` |
| Cosmos DB InMemory | `GetAggregate` (cold build), `UpdateAggregate`, `SaveAggregate` |
| Entity Framework Core (DCB) | `GetAggregate` (cold build), `UpdateAggregate` |

Reading an aggregate with `ReadMode.SnapshotOnly` does not fold anything, so it emits nothing.

#### From the DCB store

The [DCB store](configuration/dcb-ef-core.md) emits the **same event name**, so one query finds folds
in either consistency model. Three tags differ, because the concepts do:

| Tag | Type | Meaning |
|---|---|---|
| `tagQuery` | string | The boundary the events were read from, canonical form — replaces `streamId` |
| `aggregateId` | string | Model store id, as above |
| `appliedFromPosition` | long | Global position of the first event folded — replaces `appliedFromSequence` |
| `appliedToPosition` | long | Global position of the last event folded — replaces `appliedToSequence` |
| `appliedCount` | int | As above |
| `versionBefore` | int | As above |
| `versionAfter` | int | As above |

Emitted from `GetAggregate` (cold build), from `UpdateAggregate`, and from the snapshot refresh under
`ReadMode.SnapshotWithNewEvents` — which is the same refresh `UpdateAggregate` performs.

### Projection Folded

Emitted by every store whenever it folds events into a projection and writes the resulting snapshot.
A read model differs from a write model only in never producing events, so folding one is worth
exactly as much to a trace as folding the other, and it is recorded the same way.

| Tag | Type | Meaning |
|---|---|---|
| `streamId` | string | The stream the events were read from |
| `projectionId` | string | Projection store id, `{IProjectionId.Id}:{[ProjectionType] version}` |
| `appliedFromSequence` | int | Sequence of the first event folded |
| `appliedToSequence` | int | Sequence of the last event folded |
| `appliedCount` | int | How many events were folded |
| `versionBefore` | int | The projection's version before the fold |
| `versionAfter` | int | The projection's version after the fold |

`appliedCount` against `versionAfter - versionBefore` reads exactly as it does above.

Emitted from:

| Store | Operations |
|---|---|
| Entity Framework Core | `GetProjection` (cold build), `UpdateProjection` |
| Cosmos DB | `GetProjection` (cold build), `UpdateProjection` |
| Cosmos DB InMemory | `GetProjection` (cold build), `UpdateProjection` |
| Entity Framework Core (DCB) | `GetProjection` (cold build), `UpdateProjection` |

The DCB store substitutes `tagQuery` for `streamId` and positions for sequences, as it does for
aggregate folds.

> **Why a separate event name.** The tag shapes match apart from the identifier, so a query across
> both models is a two-name filter. Calling a projection fold `Aggregate Folded` would have avoided
> that at the cost of making the name wrong about half its occurrences — the same reasoning that
> keeps [Concurrency Conflict](#concurrency-conflict) apart from
> [Concurrency Exception](#concurrency-exception).

### Concurrency Exception

Emitted by both stores when a write's `expectedEventSequence` does not match the stream's current
sequence. The write is refused; see [Result pattern](../concepts/result-pattern.md).

| Tag | Type | Meaning |
|---|---|---|
| `streamId` | string | The stream |
| `expectedEventSequence` | int | The sequence the caller expected |
| `latestEventSequence` | int | The sequence the stream is actually at |

### Concurrency Conflict

The DCB store's equivalent, emitted when an append's boundary moved between the decision reading it
and the write. Named differently from `Concurrency Exception` on purpose: the two carry different
tags, and merging them would make either name a lie about half its occurrences.

| Tag | Type | Meaning |
|---|---|---|
| `tagQuery` | string | The boundary that was asserted over |
| `expectedPosition` | long | The position the decision read |
| `latestPosition` | long | The position the boundary is actually at |

The refusal itself is `memoria/concurrency-conflict` — the same failure type a stream conflict
produces, so a retry policy keyed on it works against both models.

### NoUncommittedEvents

Entity Framework Core only. Emitted when `SaveAggregate` is called with an aggregate that has no
uncommitted events. This is a no-op, not a failure — the event distinguishes "there was nothing to
save" from "the save did not run".

| Tag | Type | Meaning |
|---|---|---|
| `streamId` | string | The stream |
| `aggregateId` | string | The aggregate store id |

### Cosmos Transactional Batch, Cosmos Read Item, Cosmos Feed Iterator

Cosmos DB only. Transport-level records of the calls the store makes, carrying the request charge
that RU budgeting needs. All three carry `operation` (the store operation that issued the call),
`streamId`, `cosmos.activityId`, `cosmos.statusCode`, and `cosmos.requestCharge`.

| Event | Additional tags |
|---|---|
| `Cosmos Transactional Batch` | `cosmos.errorMessage`, `cosmos.count`, and either `aggregateId` or `eventDocumentIds` |
| `Cosmos Read Item` | `aggregateId`, on aggregate reads only |
| `Cosmos Feed Iterator` | `cosmos.count` |

The InMemory Cosmos store makes no Cosmos calls, so it emits none of these. It does emit
`Aggregate Folded` and `Concurrency Exception`, so telemetry that depends only on those can be
exercised without an emulator.

### Exceptions

Both stores record store exceptions on the current activity with
`Activity.AddException`, tagged `operation` and `streamId`, before returning a
[storage failure](../concepts/result-pattern.md#failure-classification). Provider messages that the
`Result` does not carry — a constraint name, a provider error code — can be read there.

## Worked example: an aggregate is in a state nobody expects

1. Find the `Aggregate Folded` events for that `aggregateId` in your tracing tool.
2. Read `appliedFromSequence` and `appliedToSequence`. Those are the events that built the state.
   Fetch them with `GetEvents`, or read them from the store directly.
3. Compare `appliedCount` against `versionAfter - versionBefore`. If `appliedCount` is larger, the
   fold read events the aggregate ignored — check the aggregate's `Apply` for the types in that
   range before assuming the events themselves are wrong.
4. Check `versionBefore`. If it is not what the previous fold left behind, an intervening write is
   missing from the trace.

## Limits

- **These are activity events, not metrics.** They are sampled and retained by your tracing backend's
  policy, typically days to weeks. They are not a durable audit trail. The event stream is the
  durable record; this is the annotation over it.
- **`Aggregate Folded` gives counts, not identities.** `versionAfter - versionBefore` tells you how
  many folded events changed the aggregate, not which ones. The store's fold does not expose that.
- **Tag payloads are bounded, with one exception.** Every `Aggregate Folded` tag is a scalar, so a
  fold over a thousand events costs the same as one over two. `Cosmos Transactional Batch` for
  `SaveEvents` carries `eventDocumentIds`, which grows with the batch — up to 100 ids, the Cosmos
  transactional batch limit.
- **Nothing is emitted without an ambient activity.** If `Activity.Current` is `null` — no
  instrumentation, or work on a thread the activity did not flow to — the store records nothing and
  reports nothing about that.
