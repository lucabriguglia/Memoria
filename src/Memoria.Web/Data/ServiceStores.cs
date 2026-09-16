using System.Collections.Concurrent;
using Memoria.EventSourcing;
using Memoria.EventSourcing.Dcb;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;
using Memoria.EventSourcing.Store.EntityFrameworkCore;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Filtering;
using Memoria.Web.Extensibility;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Memoria.Web.Data;

/// <summary>
/// Every service's store, built from what the configuration says about the connection string
/// each names and from the bindings the registry built for each — once per service and catalogue,
/// and again after a reload, since an upload may bring a new service or change what one binds.
/// </summary>
/// <param name="configuration">Where the connection strings and their settings are.</param>
/// <param name="registry">Where the services and their bindings come from.</param>
/// <param name="clock">What the totals caches count time by.</param>
/// <remarks>
/// A Cosmos client is one per account for the life of the process, whichever services read it:
/// the client is the expensive thing, and two services over one account share it.
/// </remarks>
public sealed class ServiceStores(IConfiguration configuration, DomainTypeRegistry registry, TimeProvider clock)
{
    private readonly ConcurrentDictionary<string, (DomainTypeCatalogue From, ServiceStore Store)> _stores =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, CosmosClient> _clients = new(StringComparer.Ordinal);

    /// <summary>The store behind one service, as of the current catalogue.</summary>
    public ServiceStore For(Service service)
    {
        var catalogue = registry.Current;

        if (_stores.TryGetValue(service.Slug, out var held) && ReferenceEquals(held.From, catalogue))
        {
            return held.Store;
        }

        var connection = ConnectionStrings.Named(configuration, service.ConnectionString);
        var client = connection.Database?.Provider is DatabaseProvider.Cosmos
            ? _clients.GetOrAdd(connection.Database.ConnectionString, connectionString => new CosmosClient(
                connectionString, new CosmosClientOptions
                {
                    // Questions are asked across streams, so most reads are cross-partition
                    // queries. Gateway mode is the one that works wherever this is run, including
                    // from behind the proxies an operator's machine tends to sit behind.
                    ConnectionMode = ConnectionMode.Gateway
                }))
            : null;

        var store = new ServiceStore(service, connection, catalogue.BindingsOf(service), new TotalsCache(clock), client);
        _stores[service.Slug] = (catalogue, store);

        return store;
    }

    /// <summary>Every service's store, in the order the services are listed.</summary>
    public IReadOnlyList<ServiceStore> All() => registry.Current.Services.Select(For).ToList();
}

/// <summary>
/// Registration of the per-service stores, and the middleware that puts a request inside the
/// service its address names.
/// </summary>
public static class ServiceStoreRegistration
{
    /// <summary>
    /// Registers the stores, and everything a page or a post under a service resolves from the
    /// one the request is inside: its readers, its contexts, its domain services, its
    /// capabilities and its totals.
    /// </summary>
    /// <remarks>
    /// Scoped, from the request's <see cref="CurrentService"/>: a request is under one service or
    /// none, and outside one there is nothing to resolve. The context options are registered on
    /// their own so a test can replace where a context opens without replacing the context.
    /// </remarks>
    public static IServiceCollection AddStores(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.TryAddSingleton<IEventDataFilter, SubstringEventDataFilter>();
        services.AddSingleton<ServiceStores>();

        services.AddScoped(provider =>
        {
            var current = provider.GetRequiredService<CurrentService>();

            return provider.GetRequiredService<ServiceStores>().For(
                current.Service ?? throw new InvalidOperationException("This request is under no service, so it has no store."));
        });

        services.AddScoped(provider => provider.GetRequiredService<ServiceStore>().StreamedOptions(provider));
        services.AddScoped(provider => provider.GetRequiredService<ServiceStore>().DcbOptions(provider));

        services.AddScoped(provider => provider.GetRequiredService<ServiceStore>().NewStreamedContext(provider));
        services.AddScoped<IDomainDbContext>(provider => provider.GetRequiredService<StreamedStoreDbContext>());
        services.AddScoped(provider => provider.GetRequiredService<ServiceStore>().NewDcbContext(provider));
        services.AddScoped<IDcbDbContext>(provider => provider.GetRequiredService<DcbStoreDbContext>());

        services.AddScoped(provider => provider.GetRequiredService<ServiceStore>().NewReads(provider));
        services.AddScoped(provider => provider.GetRequiredService<ServiceStore>().NewDomainService(provider));
        services.AddScoped(provider => provider.GetRequiredService<ServiceStore>().NewDcbDomainService(provider));
        services.AddScoped(provider => provider.GetRequiredService<ServiceStore>().Capabilities);
        services.AddScoped(provider => provider.GetRequiredService<ServiceStore>().Totals);

        return services;
    }

    /// <summary>
    /// Puts each request inside the service its first segment names, before anything answers it
    /// — so a form post under a service resolves that service's store the way a page does, and a
    /// name no manifest declares leaves the request outside every service for the router to answer.
    /// </summary>
    public static IApplicationBuilder UseServiceScope(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var segment = context.Request.Path.Value?
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();

            if (segment is not null)
            {
                var types = context.RequestServices.GetRequiredService<DomainTypeRegistry>();

                if (types.Current.ServiceAt(segment) is { } service)
                {
                    context.RequestServices.GetRequiredService<CurrentService>().Enter(service, types.Current);
                }
            }

            await next(context);
        });
}
