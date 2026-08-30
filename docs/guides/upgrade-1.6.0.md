# Upgrade to 1.6.0

Two changes need attention. Both affect only the Cosmos DB store.

1. [**Containers are created with an indexing policy**](#cosmos-containers-get-an-indexing-policy)
   — nothing to do unless you query the container yourself. Existing containers are untouched.
2. [**The Cosmos DB client is now shared**](#cosmos-client-is-now-shared) — only if you construct
   `CosmosDataStore`, `CosmosDomainService` or `CosmosSetup` yourself.

<a name="cosmos-containers-get-an-indexing-policy"></a>
## Cosmos DB containers are created with an indexing policy

Only affects the Cosmos DB store, and only containers that `CosmosSetup` creates from now on.

`CreateDatabaseAndContainerIfNotExist` used to create the container with the Cosmos DB default
indexing policy, which indexes every path of every document — including the serialised `data`
payload, the largest property in the document, which no Memoria query can filter on. It now creates
the container with a policy covering only the paths the store filters or sorts on. Measured against
the emulator that is about 2.4% off writes and 3–6% off reads.

**Existing containers are untouched.** The policy applies only to a container this call creates, so
upgrading changes nothing about a database you already have. To bring an existing container across,
ask for it explicitly:

```C#
await cosmosSetup.ReplaceIndexingPolicy(CosmosIndexingPolicy.CreateRecommended());
```

That starts a background reindex: the container stays online and writes keep succeeding, but queries
can return incomplete results until it finishes. Do it during a quiet period on a container that
holds data. The equivalent scripts under `scripts/install` do the same thing through the Azure CLI.

**If you query the container yourself**, check your queries first. Filtering or sorting on a path
the policy excludes still returns correct results, but scans the partition instead of using an
index. Either add the path, or keep the previous behaviour by passing a policy of your own:

```C#
await cosmosSetup.CreateDatabaseAndContainerIfNotExist(new IndexingPolicy());
```

See [Tune the Cosmos DB container](tune-the-cosmos-container.md) for which paths are indexed and
why, including why there are no composite indexes.

<a name="cosmos-client-is-now-shared"></a>
## The Cosmos DB client is now shared

Previously `CosmosDataStore` and `CosmosDomainService` each built their own `CosmosClient` in their
constructor, and each disposed it. Both are registered with a scoped lifetime, so an ASP.NET Core
application created and destroyed two clients per request, and `CosmosSetup` created a third on every
call.

A `CosmosClient` is designed to live for the lifetime of the application: it performs account
discovery, builds a routing map, and — in `Direct` mode, the Memoria default — opens its own
connections to every replica it touches. None of that survives disposal, so every request paid the
warm-up again.

One `CosmosClient` is now created for the application and shared. It is owned by a new
`CosmosClientProvider`, which `AddMemoriaCosmos` registers as a singleton.

### If you register through `AddMemoriaCosmos`

Nothing to do. The wiring changed underneath you.

### If you construct the types yourself

Both constructors now take a `CosmosClientProvider` in place of `IOptions<CosmosOptions>`, and so
does `CosmosSetup`:

```C#
// Before
var dataStore = new CosmosDataStore(options, timeProvider, httpContextAccessor);
var domainService = new CosmosDomainService(options, timeProvider, httpContextAccessor, dataStore);
var setup = new CosmosSetup(options);

// After
var clientProvider = new CosmosClientProvider(options);
var dataStore = new CosmosDataStore(clientProvider, timeProvider, httpContextAccessor);
var domainService = new CosmosDomainService(clientProvider, timeProvider, httpContextAccessor, dataStore);
var setup = new CosmosSetup(options, clientProvider);
```

Create one `CosmosClientProvider` and share it, as the example does — one per call would reintroduce
exactly the cost this change removes. Dispose it when the application shuts down; the dependency
injection container does that for you.

Resolve `CosmosClientProvider` if you need the shared `CosmosClient` or `Container` directly.

### Two consequences worth knowing

**`Dispose()` on the store types no longer does anything.** `CosmosDataStore` and
`CosmosDomainService` still implement `IDisposable`, because `ICosmosDataStore` and `IDomainService`
declare it, but the client they used to dispose is not theirs to close. Existing `using` blocks stay
correct and now stop tearing down connections other scopes are still using.

**Options are read once.** The client is built on first resolution, so changing `CosmosOptions`
afterwards no longer affects the connection it holds. Previously each scope picked up the current
options. If you relied on reconfiguring the endpoint, key or client options at runtime, that no
longer takes effect.

## Related

- [Cosmos DB configuration](../reference/configuration/cosmos.md)
- [Tune the Cosmos DB container](tune-the-cosmos-container.md)
- [Upgrade to 1.5.0](upgrade-1.5.0.md)
