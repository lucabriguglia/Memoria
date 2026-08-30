# Upgrade to 1.6.0

One change needs attention, and it only affects the Cosmos DB store.

1. [**The Cosmos DB client is now shared**](#cosmos-client-is-now-shared) — only if you construct
   `CosmosDataStore`, `CosmosDomainService` or `CosmosSetup` yourself.

Nothing else needs action. The Cosmos DB container gains a recommended indexing policy, but it ships
as a script you choose to apply rather than a change the package makes for you — see
[Tune the Cosmos DB container](tune-the-cosmos-container.md).

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
