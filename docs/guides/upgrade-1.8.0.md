# Upgrade to 1.8.0

Memoria 1.8.0 adds Dynamic Consistency Boundaries (DCB) as a second consistency model, in its own
packages. To make room for it, one property moves one level down the model hierarchy.

1. [**`StreamId` moves off `IEventSourcedModel`**](#streamid-moves-off-ieventsourcedmodel) — only if
   you declare a variable, parameter or field as `IEventSourcedModel` and read `StreamId` from it.
2. [**Nothing else changes**](#nothing-else-changes) — DCB is additive and opt-in.

No event, aggregate snapshot or projection is rewritten, no schema changes, and no data is migrated.
If you do not use `IEventSourcedModel` by name, upgrading is a version bump.

<a name="streamid-moves-off-ieventsourcedmodel"></a>
## `StreamId` moves off `IEventSourcedModel`

`StreamId` was declared on `IEventSourcedModel` and `EventSourcedModel` — the base shared by
`AggregateRoot` and `Projection`. It now lives on a new `IStreamedModel` / `StreamedModel` layer
inserted between them:

```
IEventSourcedModel        Version, LatestEventSequence, EventTypeFilter, Apply, IsEventHandled
 └ IStreamedModel        + StreamId                                                   // new
    ├ IAggregateRoot     + AggregateId, UncommittedEvents
    └ IProjection        + ProjectionId
```

`AggregateRoot` and `Projection` now derive from `StreamedModel` rather than `EventSourcedModel`.

### What is unaffected

Almost certainly your code. `StreamId` is still on `AggregateRoot`, `Projection`, `IAggregateRoot`
and `IProjection`, in the same namespace, with the same type and the same `[JsonIgnore]`. Your
aggregates and projections need no change, `IDomainService` is untouched, and every store behaves
identically:

```csharp
public class OrderAggregate : AggregateRoot { /* unchanged */ }

var order = (await domainService.GetAggregate(streamId, aggregateId)).Value;
var id = order.StreamId;              // still compiles
IAggregateRoot root = order;
var alsoId = root.StreamId;           // still compiles
```

### What breaks

Only code that names `IEventSourcedModel` itself and reads `StreamId` through it:

```csharp
// 1.7.0
static string Describe(IEventSourcedModel model) => model.StreamId;

// 1.8.0 — widen to the streamed layer
static string Describe(IStreamedModel model) => model.StreamId;
```

The compiler catches every occurrence; there is no silent behaviour change to look for. If a method
takes `IEventSourcedModel` but does not touch `StreamId`, leave it — it now correctly accepts DCB
models too.

### Why it moved

DCB has no streams. Its consistency boundary is a tag query evaluated at append time, so a DCB
aggregate has no stream to belong to. Everything else about an event-sourced model is unchanged by
that choice — version tracking, the event type filter, and the fold in `Apply` are the same work
whichever consistency model you pick, and DCB reuses them rather than reimplementing them.

Leaving `StreamId` on the shared base would have given every DCB model a public, settable property
that means nothing, with nothing to stop a consumer reading it and no honest value to put in it.
Moving it down one level keeps the fold shared and the identity specific.

<a name="nothing-else-changes"></a>
## Nothing else changes

DCB ships as separate packages — `Memoria.EventSourcing.Dcb` and
`Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore`. `Memoria.EventSourcing` does not reference
them, so installing the streams packages pulls in nothing DCB. If you are not adopting DCB, this
release is the property move above and nothing more.

To choose between the two models, see
[Streams or DCB](choose-streams-or-dcb.md).
