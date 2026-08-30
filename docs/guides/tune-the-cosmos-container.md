# Tune the Cosmos DB container

The Cosmos DB **default indexing policy** indexes every path of every document. For an event store
that is the wrong default: the `data` property holds a serialised domain event or snapshot, it is
the largest property in the document, and no query Memoria issues can filter on it — `CONTAINS`
never uses the index. Every write pays to index it anyway.

`CosmosSetup.CreateDatabaseAndContainerIfNotExist` already creates containers with the better
policy, so **if Memoria provisions your container there is nothing to do here**. This guide is for
containers provisioned elsewhere — infrastructure as code, a portal, a DBA — and for containers that
already exist, which keep whatever policy they were created with.

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
| `streamId` | the partition predicate in each `WHERE` clause, and the `MAX(sequence)` aggregate |

> **Do not remove `/streamId`.** Every query already scopes itself with a partition key, so indexing
> the partition key path looks redundant. Measured, excluding it takes 4.9% off writes but costs
> **119% more** on `SELECT VALUE MAX(c.sequence)` — the concurrency check that runs on every
> `SaveAggregate` and `SaveEvents`. That is +4.43 RU per save against a saving of about 0.38 RU per
> event written, so it loses for anything but enormous batches.

Nothing else is queried. `data`, `version`, `latestEventSequence`, `aggregateType`,
`projectionType`, `eventId`, `createdBy`, `updatedBy`, and `updatedDate` are read from the returned
documents, never filtered or sorted on.

## Apply the policy

The policy lives at
[`scripts/install/1.6.0-cosmos-indexing-policy.json`](../../scripts/install/1.6.0-cosmos-indexing-policy.json).
It excludes `/*` and includes only the paths in the table above. It defines no composite indexes —
see [why there are no composite indexes](#no-composite-indexes) below, which is a measurement, not
an oversight.

`id` is not listed: Cosmos DB always indexes it and rejects a policy that tries to override it.

> **Applies to 1.5.0 as well.** The file is named for the release it ships in, but it is a container
> setting, not package content. The query shapes it serves are unchanged since 1.5.0, so applying it
> to a 1.5.0 deployment is safe and worthwhile.

For an Azure account, run either script — they do the same thing:

```powershell
./scripts/install/1.6.0-cosmos-apply-indexing-policy.ps1 `
    -ResourceGroup rg-shop -Account cosmos-shop -Wait
```

```bash
./scripts/install/1.6.0-cosmos-apply-indexing-policy.sh \
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

The Azure CLI cannot reach the Cosmos DB emulator, so use the API instead:

```C#
await cosmosSetup.ReplaceIndexingPolicy(CosmosIndexingPolicy.CreateRecommended());
```

That works against any account, not just the emulator. You can also paste the JSON into
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

<a name="no-composite-indexes"></a>
### Why there are no composite indexes

An earlier draft of this policy defined three, on `(documentType, sequence)`,
`(documentType, createdDate)` and `(documentType, aggregateId, appliedDate)`, reasoning that each
served a filter-and-order-by read the store issues. Measured, they cost more than they returned.

Against the Cosmos DB emulator: 200 event documents of roughly 600 bytes written in two batches,
then each read issued once. Request charge, against the default index-everything policy:

| | Write 200 events | Read whole stream | `MAX(sequence)` | Sequence range | Date range | Type filter |
|---|---|---|---|---|---|---|
| Default policy | 1600.00 | 10.54 | 3.55 | 6.89 | 11.11 | 11.37 |
| **Exclusions only (shipped)** | **1561.90** | **9.94** | 3.71 | **6.69** | **10.55** | **10.77** |
| Composites only | 1714.28 | 10.54 | 3.63 | 6.69 | 10.75 | 10.87 |
| Exclusions + composites | 1676.20 | 9.94 | 3.55 | 6.59 | 10.55 | 10.77 |

The composite indexes add about **7% to every write** and return essentially nothing: the reads are
as cheap with exclusions alone. Every query here is single-partition with an equality filter on
`documentType` and an `ORDER BY c.sequence`, and within one partition the range index on
`/sequence` already serves that ordering — so the composite is maintained on every write and then
not used.

Two caveats on those numbers. They come from the emulator, not a real account, and from one payload
shape on one partition of 200 events. A workload with much larger partitions or more selective
filters could tip the other way. If you think yours might, add a composite index back and measure
before keeping it — turn on
[index metrics](https://learn.microsoft.com/azure/cosmos-db/nosql/index-metrics) with
`QueryRequestOptions.PopulateIndexMetrics` and compare `RequestCharge`.

The exclusions are the part that pays, and they pay on both sides: writes drop 2.4% and reads 3–6%.

## Measuring the effect

Memoria already reports the RU charge of every operation. Each `Cosmos Read Item`,
`Cosmos Feed Iterator`, and `Cosmos Transactional Batch` activity event carries a
`cosmos.requestCharge` tag — see [Cosmos DB configuration](../reference/configuration/cosmos.md).
Capture those before and after applying the policy; the write path (`Save Aggregate`,
`Save Events`) is where the largest change should show.

## Related

- [Cosmos DB configuration](../reference/configuration/cosmos.md)
- [Install the store schema](install-the-store-schema.md) — the Entity Framework Core equivalent
