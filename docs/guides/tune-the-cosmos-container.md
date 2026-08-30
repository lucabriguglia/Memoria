# Tune the Cosmos DB container

`CosmosSetup.CreateDatabaseAndContainerIfNotExist` creates the container with the Cosmos DB
**default indexing policy**, which indexes every path of every document. For an event store that is
the wrong default: the `data` property holds a serialised domain event or snapshot, it is the
largest property in the document, and no query Memoria issues can filter on it — `CONTAINS` never
uses the index. Every write pays to index it anyway.

This guide replaces that policy with one built for the queries the store actually issues.

## What the store queries

All Memoria containers use `/streamId` as the partition key, and every read passes the partition key
in the request options, so each query is scoped to one logical partition. Within that partition the
store filters and sorts on a small, fixed set of paths:

| Path | Used by |
|------|---------|
| `documentType` | every query — the container mixes events, aggregates, aggregate-event links, and projections |
| `sequence` | event range reads, `ORDER BY`, and `SELECT VALUE MAX(c.sequence)` on every save |
| `createdDate` | the date-bounded event reads (`GetEventsUpToDate`, `FromDate`, `BetweenDates`) |
| `aggregateId`, `appliedDate` | `GetEventsAppliedToAggregate` |
| `eventType` | the `EventTypeFilter` on aggregates and projections |
| `id` | fetching specific events by identifier |
| `streamId` | the redundant-but-present partition predicate in each `WHERE` clause |

Nothing else is queried. `data`, `version`, `latestEventSequence`, `aggregateType`,
`projectionType`, `eventId`, `createdBy`, `updatedBy`, and `updatedDate` are read from the returned
documents, never filtered or sorted on.

## Apply the policy

The policy lives at
[`scripts/install/1.5.0-cosmos-indexing-policy.json`](../../scripts/install/1.5.0-cosmos-indexing-policy.json).
It excludes `/*` and includes only the paths in the table above, plus three composite indexes for
the filter-and-order-by reads.

For an Azure account, run either script — they do the same thing:

```powershell
./scripts/install/1.5.0-cosmos-apply-indexing-policy.ps1 `
    -ResourceGroup rg-shop -Account cosmos-shop -Wait
```

```bash
./scripts/install/1.5.0-cosmos-apply-indexing-policy.sh \
    --resource-group rg-shop --account cosmos-shop --wait
```

Both default to database `Memoria` and container `Domain`, matching `CosmosOptions`. Pass
`-Database`/`--database` and `-Container`/`--container` if you changed them. Both require the
[Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) and an `az login` that can
write to the account.

Applying the same policy twice is a no-op, so the scripts are safe in a deployment pipeline.

If you provision infrastructure declaratively, take the JSON straight into your template instead —
`Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers` accepts it verbatim under
`properties.resource.indexingPolicy` in Bicep and ARM, as does `indexing_policy` in Terraform's
`azurerm_cosmosdb_sql_container`.

### The emulator

The Azure CLI cannot reach the Cosmos DB emulator. For local development, paste the JSON into
**Data Explorer → your container → Settings → Indexing Policy → Save**, or delete and recreate the
container.

## What happens when you apply it

Cosmos DB reindexes in the background. The container stays online and writes keep succeeding, but
**queries can return incomplete results until the transformation finishes**, so apply it during a
quiet period on a container that already holds data. Both scripts accept `--wait` / `-Wait` to poll
`indexTransformationProgress` until it reaches 100%.

There is no rollback script. To go back, apply the Cosmos DB default policy:

```json
{
  "indexingMode": "consistent",
  "automatic": true,
  "includedPaths": [{ "path": "/*" }],
  "excludedPaths": [{ "path": "/\"_etag\"/?" }]
}
```

## If you query the container yourself

Because the policy excludes `/*`, a query of your own that filters on an unlisted path — say
`c.aggregateType` or `c.updatedBy` — will fall back to a scan of the partition rather than fail.
That is correct but expensive. Add the path to `includedPaths` before relying on it:

```json
{ "path": "/aggregateType/?" }
```

Keep `/data` excluded regardless. It is a serialised string, and `CONTAINS(c.data, ...)` — which is
how `eventPropertyFilter` is translated — cannot use an index in any case, so indexing it buys
nothing and costs write RU on every event.

### An optional fourth composite index

Workloads whose aggregates and projections declare a narrow `EventTypeFilter` issue
`ARRAY_CONTAINS(@eventTypes, c.eventType)` alongside `ORDER BY c.sequence` on most reads. Adding

```json
[
  { "path": "/documentType", "order": "ascending" },
  { "path": "/eventType", "order": "ascending" },
  { "path": "/sequence", "order": "ascending" }
]
```

may cut the RU charge on those reads. It is not in the shipped policy because it applies to every
event document and so raises write cost for everyone, while only paying back for type-filtered
reads. Measure before adding it: turn on
[index metrics](https://learn.microsoft.com/azure/cosmos-db/nosql/index-metrics) with
`QueryRequestOptions.PopulateIndexMetrics` and compare `RequestCharge` with and without.

## Measuring the effect

Memoria already reports the RU charge of every operation. Each `Cosmos Read Item`,
`Cosmos Feed Iterator`, and `Cosmos Transactional Batch` activity event carries a
`cosmos.requestCharge` tag — see [Cosmos DB configuration](../reference/configuration/cosmos.md).
Capture those before and after applying the policy; the write path (`Save Aggregate`,
`Save Events`) is where the largest change should show.

## Related

- [Cosmos DB configuration](../reference/configuration/cosmos.md)
- [Install the store schema](install-the-store-schema.md) — the Entity Framework Core equivalent
