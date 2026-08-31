# Upgrade to 1.7.0

Memoria 1.7.0 removes the link between aggregates and events — the Entity Framework Core
`DomainAggregateEvents` table and the Cosmos DB `AggregateEvent` document. Four things need
attention, and only the first is likely to affect application code.

1. [**`GetEventsAppliedToAggregate` is removed**](#geteventsappliedtoaggregate-is-removed) — there is
   no replacement. Read this one even if you skip the rest.
2. [**Entity Framework Core store contracts changed**](#entity-framework-core-store-contracts-changed)
   — only if you implement `IDomainDbContext` or call the tracking extensions directly.
3. [**Cosmos DB data store contracts changed**](#cosmos-db-data-store-contracts-changed) — only if
   you use `ICosmosDataStore` directly.
4. [**Drop the retired table**](#drop-the-retired-table) and
   [**apply the new Cosmos DB policy**](#apply-the-new-cosmos-db-indexing-policy) — housekeeping,
   at your convenience.

Nothing in the event stream changes. No event, aggregate snapshot or projection is rewritten, and no
data is migrated. Upgrading and leaving every database exactly as it is works: the retired table
simply stops being written to.

<a name="geteventsappliedtoaggregate-is-removed"></a>
## `GetEventsAppliedToAggregate` is removed

```csharp
// Gone in 1.7.0
var eventsResult = await domainService.GetEventsAppliedToAggregate(streamId, aggregateId);
```

Also removed: `IDomainDbContextExtensions.GetEventsAppliedToAggregate`,
`GetEventEntitiesAppliedToAggregate` and `GetAggregateEventEntities` on the Entity Framework Core
store, and `ICosmosDataStore.GetAggregateEventDocuments` on the Cosmos DB store.

### Why it could not be kept

An aggregate's store id is `{IAggregateId.Id}:{[AggregateType] version}`. Bump the version on a CLR
type and it takes on a new store identity, while snapshots written under the old one stay in the
store:

```csharp
[AggregateType("Order", version: 2)]   // was version: 1
public class OrderAggregate : AggregateRoot
{
    public override Type[] EventTypeFilter { get; } = [ /* today's filter */ ];
}
```

There is only ever **one** CLR type, so `EventTypeFilter` only ever reports today's filter. Any
attempt to recompute which events built a `{id}:1` snapshot would apply version 2's filter to it and
return a different set of events — with no error and no warning.

The link did not have that problem: it recorded the actual event ids at the moment they were
applied. That property cannot be rebuilt from the event stream afterwards, so the method is removed
rather than reimplemented as something that looks right and quietly is not.

### If you used it for debugging

That is what it was for, and it is replaced — see [Observability](../reference/observability.md).
Every snapshot write now records an `Aggregate Folded` activity event carrying the sequence range it
consumed and the aggregate version either side of the fold:

| Tag | Meaning |
|---|---|
| `appliedFromSequence` | Sequence of the first event folded |
| `appliedToSequence` | Sequence of the last event folded |
| `appliedCount` | How many events were folded |
| `versionBefore` | The aggregate's version before the fold |
| `versionAfter` | The aggregate's version after the fold |

This is better evidence than the link was, for three reasons. It is written at the moment of the
fold, so a later `[AggregateType]` bump cannot invalidate it. It covers every snapshot write, where
the link was never written by the default `ReadMode.SnapshotOnly` path. And `appliedCount` against
`versionAfter - versionBefore` distinguishes events the fold *consumed* from events that actually
*changed* the aggregate — the link recorded both identically, so it could not tell you a linked
event had been a no-op.

The trade is retention. Activity events live as long as your tracing backend keeps them, typically
days to weeks; the table lived forever. The events themselves are still durable, and they are what
reconstructs state.

### If you need it durably

Build it as a projection you own:

```csharp
[ProjectionType("AppliedEvents", version: 1)]
public class AppliedEventsProjection : Projection
{
    // ...record what you need, when you need it
}
```

That puts the versioning problem where it can actually be solved: your `[ProjectionType]` version is
yours to control, so you decide what a bump means for history you have already recorded.

`GetEvents(streamId, eventTypeFilter)` is the nearest primitive, but it is **not** an equivalent —
it carries exactly the version caveat that made recomputation unsound above. Use it knowing that.

<a name="entity-framework-core-store-contracts-changed"></a>
## Entity Framework Core store contracts changed

Only affects you if you implement `IDomainDbContext` yourself or call the tracking extensions
directly. Deriving from `DomainDbContext` and using `IDomainService` needs no change.

**The `AggregateEvents` `DbSet` is gone** from `IDomainDbContext`, `DomainDbContext` and
`IdentityDomainDbContext`. Remove it from your own implementation:

```csharp
public class EventStoreDbContext : DbContext, IDomainDbContext
{
    public DbSet<AggregateEntity> Aggregates { get; set; } = null!;
    public DbSet<EventEntity> Events { get; set; } = null!;
    public DbSet<ProjectionEntity> Projections { get; set; } = null!;
-   public DbSet<AggregateEventEntity> AggregateEvents { get; set; } = null!;
}
```

`AggregateEventEntity` and `IApplicableEntity` are removed. If you implemented `IApplicableEntity`
on a type of your own, it and its `AppliedDate` handling in `AuditInterceptor` are gone; use
`IAuditableEntity` or `IEditableEntity` instead.

**Two tracking extensions changed shape**:

```csharp
// TrackAggregate: 3-tuple -> 2-tuple
var (eventEntities, aggregateEntity) =
    (await dbContext.TrackAggregate(streamId, aggregateId, aggregate, expectedEventSequence)).Value;

// TrackEventEntities: 2-tuple -> the aggregate entity alone
var aggregateEntity =
    (await dbContext.TrackEventEntities(streamId, aggregateId, eventEntities, expectedEventSequence)).Value;
```

Both dropped their `AggregateEventEntities` member. The compiler will find every call site.

<a name="cosmos-db-data-store-contracts-changed"></a>
## Cosmos DB data store contracts changed

Only affects you if you use `ICosmosDataStore` directly. Using `IDomainService` needs no change.

Two members are removed:

| Removed | Note |
|---|---|
| `GetAggregateEventDocuments(streamId, aggregateId)` | Read the retired link documents |
| `GetEventDocuments(streamId, eventIds)` | Existed to resolve those links back to events |

`AggregateEventDocument` and `DocumentType.AggregateEvent` are removed with them. The other
`GetEventDocuments` overload — the one taking an event type filter — is unchanged.

**`SaveAggregate` now accepts almost twice as many uncommitted events.** Each event used to cost two
of the 100 operations a Cosmos DB transactional batch allows, because it wrote a link document as
well. The ceiling rises from 49 to 99. Nothing breaks if you were staying under 49; if you were
splitting saves to stay beneath it, you may no longer need to. See
[Cosmos DB write limits](../reference/configuration/cosmos.md#write-limits).

<a name="drop-the-retired-table"></a>
## Drop the retired table (Entity Framework Core)

`DomainAggregateEvents` is inert after you upgrade — nothing reads or writes it. Leaving it costs
only the storage it already occupies, so there is no urgency.

**If you use EF Core migrations**, `dotnet ef migrations add` generates the drop from the model.
Nothing else to do.

**Otherwise**, run the script for your engine when you are ready:

- [`scripts/migrations/1.7.0-drop-aggregate-events-sqlserver.sql`](../../scripts/migrations/1.7.0-drop-aggregate-events-sqlserver.sql)
- [`scripts/migrations/1.7.0-drop-aggregate-events-postgresql.sql`](../../scripts/migrations/1.7.0-drop-aggregate-events-postgresql.sql)

Both are safe to run more than once. The table is the dependent side of both its foreign keys, so
the drop takes its keys and indexes with it and leaves nothing orphaned.

> **The data is not reproducible.** Those rows recorded which events were applied to which aggregate
> at the time they were applied, and that cannot be reconstructed from the event stream — which is
> the whole reason `GetEventsAppliedToAggregate` was removed rather than reimplemented. If you still
> want that history, copy the table somewhere before dropping it.

**New databases** should use the 1.7.0 install scripts, which create the three tables the store now
needs:

- [`scripts/install/1.7.0-install-sqlserver.sql`](../../scripts/install/1.7.0-install-sqlserver.sql)
- [`scripts/install/1.7.0-install-postgresql.sql`](../../scripts/install/1.7.0-install-postgresql.sql)

See [Install the store schema](install-the-store-schema.md).

### One limitation disappears

On SQL Server, `PK_DomainAggregateEvents` spanned `nvarchar(255)` plus `nvarchar(450)` — 1410 bytes
of maximum potential key against SQL Server's 900-byte limit for a clustered index key. SQL Server
permits an index whose *maximum potential* key exceeds the limit and rejects only rows whose *actual*
key does, so this surfaced at insert time: a long stream id combined with a long aggregate id failed
with `... exceeds the maximum length of 900 bytes ...`.

With the table gone, so is the constraint. If you shortened identifiers to stay under it, you no
longer need to. PostgreSQL was never affected.

<a name="apply-the-new-cosmos-db-indexing-policy"></a>
## Apply the new Cosmos DB indexing policy

The 1.6.0 policy indexes `/aggregateId` and `/appliedDate`, which existed only on link documents.
After upgrading they index nothing and still cost write RU on every document.

[`scripts/install/1.7.0-cosmos-indexing-policy.json`](../../scripts/install/1.7.0-cosmos-indexing-policy.json)
drops both. Containers that `CosmosSetup` creates from now on get it automatically. To bring an
existing container across:

```csharp
await cosmosSetup.ReplaceIndexingPolicy(CosmosIndexingPolicy.CreateRecommended());
```

Or run the Azure CLI script:

```bash
./scripts/install/1.7.0-cosmos-apply-indexing-policy.sh \
    --resource-group rg-shop --account cosmos-shop --wait
```

That starts a background reindex: the container stays online and writes keep succeeding, but queries
can return incomplete results until it finishes. Do it during a quiet period.

> **Do not apply the 1.7.0 policy to a 1.5.0 or 1.6.0 deployment.** Those versions still hold link
> documents and still query them, so dropping the two paths would leave that query scanning. The
> 1.6.0 policy remains correct for them and is still shipped.

See [Tune the Cosmos DB container](tune-the-cosmos-container.md).

## Related

- [Release notes](../release-notes.md)
- [Observability](../reference/observability.md)
- [Install the store schema](install-the-store-schema.md)
- [Tune the Cosmos DB container](tune-the-cosmos-container.md)
