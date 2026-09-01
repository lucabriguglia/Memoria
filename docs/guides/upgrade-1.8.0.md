# Upgrade to 1.8.0

Memoria 1.8.0 adds Dynamic Consistency Boundaries (DCB) as a second consistency model, in its own
packages. To make room for it, two properties move one level down the model hierarchy.

1. [**`StreamId` and `LatestEventSequence` move off
   `IEventSourcedModel`**](#streamid-and-latesteventsequence-move-off-ieventsourcedmodel) — only if
   you declare a variable, parameter or field as `IEventSourcedModel` and read either from it.
2. [**`IProjectionId` gains `EventPropertyFilter`**](#iprojectionid-gains-eventpropertyfilter) — every
   projection identifier needs one new member. One line each.
3. [**`IDomainService` gains `UpdateProjection`**](#idomainservice-gains-updateprojection) — only if
   you implement that interface yourself.
4. [**Nothing else changes**](#nothing-else-changes) — DCB is additive and opt-in.

No event, aggregate snapshot or projection is rewritten, no schema changes, and no data is migrated.
If you do not use `IEventSourcedModel` by name, upgrading is a version bump.

<a name="streamid-and-latesteventsequence-move-off-ieventsourcedmodel"></a>
## `StreamId` and `LatestEventSequence` move off `IEventSourcedModel`

Both were declared on `IEventSourcedModel` and `EventSourcedModel` — the base shared by
`AggregateRoot` and `Projection`. They now live on a new `IStreamedModel` / `StreamedModel` layer
inserted between them:

```
IEventSourcedModel        Version, EventTypeFilter, Apply, IsEventHandled
 └ IStreamedModel        + StreamId, LatestEventSequence                    // new
    ├ IAggregateRoot     + AggregateId, UncommittedEvents
    └ IProjection        + ProjectionId
```

`AggregateRoot` and `Projection` now derive from `StreamedModel` rather than `EventSourcedModel`.

### What is unaffected

Almost certainly your code. Both properties are still on `AggregateRoot`, `Projection`,
`IAggregateRoot` and `IProjection`, in the same namespace, with the same types and the same
`[JsonIgnore]`. Your aggregates and projections need no change, `IDomainService` is untouched, and
every store behaves identically:

```csharp
public class OrderAggregate : AggregateRoot { /* unchanged */ }

var order = (await domainService.GetAggregate(streamId, aggregateId)).Value;
var id = order.StreamId;              // still compiles
IAggregateRoot root = order;
var alsoId = root.StreamId;           // still compiles
```

### What breaks

Only code that names `IEventSourcedModel` itself and reads one of the two through it:

```csharp
// 1.7.0
static string Describe(IEventSourcedModel model) => $"{model.StreamId}@{model.LatestEventSequence}";

// 1.8.0 — widen to the streamed layer
static string Describe(IStreamedModel model) => $"{model.StreamId}@{model.LatestEventSequence}";
```

The compiler catches every occurrence; there is no silent behaviour change to look for. If a method
takes `IEventSourcedModel` but touches neither property, leave it — it now correctly accepts DCB
models too.

### Why they moved

DCB has no streams. Its consistency boundary is a tag query evaluated at append time, so a DCB
aggregate has no stream to belong to, and no position *within* a stream either — it records how far
it folded as a position global to the whole log. Everything else about an event-sourced model is
unchanged by that choice: version tracking, the event type filter, and the fold in `Apply` are the
same work whichever consistency model you pick, and DCB reuses them rather than reimplementing them.

Leaving these on the shared base would have given every DCB model two public, settable properties
that mean nothing to it. `LatestEventSequence` is the sharper case: it is an `int`, because a
sequence counts within one stream, whereas a DCB position counts every event in the store. Reusing
it would have put a silent ~2.1 billion event ceiling on the whole log. DCB models instead carry
`LatestPosition`, a `long`, on their own `IDcbModel` / `DcbModel` base:

```
IEventSourcedModel        Version, EventTypeFilter, Apply, IsEventHandled
 ├ IStreamedModel        + StreamId, LatestEventSequence   (int, within one stream)
 └ IDcbModel             + LatestPosition                  (long, across the whole log)
```

<a name="iprojectionid-gains-eventpropertyfilter"></a>
## `IProjectionId` gains `EventPropertyFilter`

`IAggregateId` has carried an `EventPropertyFilter` since 1.2.0, to pick one aggregate's events out
of a stream several models share. Projections had no equivalent, so a projection over a shared stream
had no way to say which events were its own. It now has the same member:

```csharp
public class OrderSummaryId(Guid orderId) : IProjectionId<OrderSummary>
{
    public string Id { get; } = orderId.ToString();

    // New. Return null when the stream holds only this projection's events.
    public IDictionary<string, string>? EventPropertyFilter => null;
}
```

**Every projection identifier needs the new member**, and the compiler names each one. Returning
`null` preserves the previous behaviour exactly — no filtering — so the mechanical fix is correct and
you only need to think about it where a stream really is shared.

To narrow, return the key/value pairs the events carry, as an aggregate identifier does:

```csharp
public IDictionary<string, string>? EventPropertyFilter { get; } =
    new Dictionary<string, string> { ["OrderId"] = orderId.ToString() };
```

The filter applies everywhere the projection is folded: the cold build, the refresh under
`SnapshotWithNewEvents`, and all three `GetInMemoryProjection` overloads, on every store.

> On PostgreSQL with `jsonb` event data, the same caveat applies as for aggregates — the default
> substring filter does not match reformatted `jsonb`. See
> [Use PostgreSQL with `jsonb`](use-postgres-jsonb.md).

<a name="idomainservice-gains-updateprojection"></a>
## `IDomainService` gains `UpdateProjection`

The projection refresh already existed and already backed `ReadMode.SnapshotWithNewEvents`; it was
just not reachable on its own, while `UpdateAggregate` was public on both `IDomainService` and the
Entity Framework Core extensions.

```csharp
var result = await domainService.UpdateProjection(streamId, projectionId);
```

Every store Memoria ships implements it. **Only a hand-rolled `IDomainService` needs the new member**
— deriving from a shipped store, or calling the extension methods, needs no change.

<a name="nothing-else-changes"></a>
## Nothing else changes

DCB ships as separate packages — `Memoria.EventSourcing.Dcb` and
`Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore`. `Memoria.EventSourcing` does not reference
them, so installing the streams packages pulls in nothing DCB. If you are not adopting DCB, this
release is the property move above and nothing more.

To choose between the two models, see
[Streams or DCB](choose-streams-or-dcb.md).
