using Memoria.EventSourcing;
using Memoria.EventSourcing.Dcb;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.Cosmos;
using Memoria.EventSourcing.Store.EntityFrameworkCore;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Filtering;
using Memoria.Web.Extensibility;
using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;

namespace Memoria.Web.Data;

/// <summary>
/// One service's store as this instance reaches it: the connection string its manifest named as
/// the configuration holds it, the bindings its rows are read through, and the readers and
/// writers built over both for a request.
/// </summary>
/// <param name="Service">The service.</param>
/// <param name="Connection">What the configuration says about the string the service names.</param>
/// <param name="Bindings">The service's own type bindings, from its own assemblies alone.</param>
/// <param name="Totals">
/// Where the service's list totals are remembered between pages — one per service, since two
/// services' lists are different lists however alike their names.
/// </param>
/// <param name="Client">The Cosmos client over the account, when the store is a Cosmos one; shared across requests.</param>
/// <remarks>
/// Built by <see cref="ServiceStores"/> once per service and catalogue, and read by the scoped
/// registrations that give the pages their <c>IStreamedReads</c>, contexts and domain services:
/// each request under a service gets those built from this, over the request's own scope. A
/// service whose string is not configured or cannot be read has a <see cref="Problem"/> and builds
/// nothing; the pages under it say so instead.
/// </remarks>
public sealed record ServiceStore(
    Service Service,
    NamedConnection Connection,
    TypeBindingSet Bindings,
    TotalsCache Totals,
    CosmosClient? Client)
{
    /// <summary>The store, when the string is configured and readable.</summary>
    public DatabaseConnection? Database => Connection.Database;

    /// <summary>Whether there is a store to read at all.</summary>
    public bool Reachable => Database is not null;

    /// <summary>
    /// Why there is nothing to read, when there is nothing: the string is not configured, or it
    /// could not be read. Null for a reachable store.
    /// </summary>
    public string? Problem => Connection switch
    {
        { Configured: false } => $"The connection string '{Connection.Name}' this service reads over is not configured.",
        { Database: null } => $"The connection string '{Connection.Name}' this service reads over could not be read: {Connection.Problem}",
        _ => null
    };

    /// <summary>What the pages under the service may offer, decided by the engine that opens its store.</summary>
    public StoreCapabilities Capabilities =>
        Database is { } database ? StoreCapabilities.Of(database.Provider) : new StoreCapabilities(HasDcb: false, CanUpdate: false);

    /// <summary>The options a streamed context over this store is built with, for one request.</summary>
    public DbContextOptions<DomainDbContext> StreamedOptions(IServiceProvider scope) =>
        Opened(new DbContextOptionsBuilder<DomainDbContext>(), scope).Options;

    /// <summary>The options a DCB context over this store is built with, for one request.</summary>
    public DbContextOptions<DcbDbContext> DcbOptions(IServiceProvider scope) =>
        Opened(new DbContextOptionsBuilder<DcbDbContext>(), scope).Options;

    /// <summary>
    /// The streamed context for one request, over the options that scope resolves — which a test
    /// may have replaced — and reading through this service's bindings.
    /// </summary>
    public StreamedStoreDbContext NewStreamedContext(IServiceProvider scope) =>
        new(scope.GetRequiredService<DbContextOptions<DomainDbContext>>(),
            scope.GetRequiredService<TimeProvider>(), scope.GetRequiredService<IHttpContextAccessor>())
        {
            TypeBindings = Bindings
        };

    /// <summary>The DCB context for one request, the same way.</summary>
    public DcbStoreDbContext NewDcbContext(IServiceProvider scope) =>
        new(scope.GetRequiredService<DbContextOptions<DcbDbContext>>(),
            scope.GetRequiredService<TimeProvider>(), scope.GetRequiredService<IHttpContextAccessor>())
        {
            TypeBindings = Bindings
        };

    /// <summary>
    /// The reader the streamed pages ask their questions of, for one request: the relational one
    /// over the request's context, or the Cosmos one over the shared client.
    /// </summary>
    public IStreamedReads NewReads(IServiceProvider scope) =>
        Database?.Provider is DatabaseProvider.Cosmos
            ? new CosmosStreamedReads(Client!, Connection.Cosmos!.DatabaseName, Connection.Cosmos.ContainerName, Bindings, Totals)
            : new EfStreamedReads(scope.GetRequiredService<StreamedStoreDbContext>(), Totals);

    /// <summary>
    /// The framework's own write path for the streamed model, for one request — what the Update
    /// tab refreshes a snapshot through — over the request's context or the shared Cosmos client,
    /// reading through this service's bindings either way.
    /// </summary>
    public IDomainService NewDomainService(IServiceProvider scope)
    {
        if (Database?.Provider is DatabaseProvider.Cosmos)
        {
            var clientProvider = new CosmosClientProvider(
                Client!, Connection.Cosmos!.DatabaseName, Connection.Cosmos.ContainerName);
            var timeProvider = scope.GetRequiredService<TimeProvider>();
            var accessor = scope.GetRequiredService<IHttpContextAccessor>();

            return new CosmosDomainService(
                clientProvider, timeProvider, accessor, new CosmosDataStore(clientProvider, timeProvider, accessor, Bindings));
        }

        return new EntityFrameworkCoreDomainService(
            scope.GetRequiredService<StreamedStoreDbContext>(), scope.GetRequiredService<IEventDataFilter>());
    }

    /// <summary>The framework's write path for the DCB model, over the request's context. Relational stores only.</summary>
    public IDcbDomainService NewDcbDomainService(IServiceProvider scope) =>
        new EntityFrameworkCoreDcbDomainService(scope.GetRequiredService<DcbStoreDbContext>());

    private TBuilder Opened<TBuilder>(TBuilder builder, IServiceProvider scope) where TBuilder : DbContextOptionsBuilder
    {
        var database = Database ?? throw new InvalidOperationException(Problem);

        database.Apply(builder).UseApplicationServiceProvider(scope);
        return builder;
    }
}
