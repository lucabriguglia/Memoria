using Memoria.EventSourcing;
using Memoria.EventSourcing.Store.Cosmos;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Memoria.Web.Data;

/// <summary>
/// Where a Cosmos store's documents are, which its connection string does not say.
/// </summary>
/// <param name="ConnectionString">The account, as given.</param>
/// <param name="DatabaseName">The database the container is in.</param>
/// <param name="ContainerName">The container the store writes into.</param>
/// <remarks>
/// A Cosmos connection string names an account and nothing more, so the database and the container
/// are asked for separately. They default to what <c>CosmosOptions</c> defaults to, so a store
/// installed under those names needs no settings at all.
/// </remarks>
public sealed record CosmosStore(string ConnectionString, string DatabaseName, string ContainerName)
{
    /// <summary>The setting holding the database name.</summary>
    public const string DatabaseSetting = "Database:Cosmos:DatabaseName";

    /// <summary>The setting holding the container name.</summary>
    public const string ContainerSetting = "Database:Cosmos:ContainerName";

    /// <summary>
    /// Reads where a Cosmos store's documents are, from the settings the one store the tool used
    /// to read has always had.
    /// </summary>
    public static CosmosStore Of(DatabaseConnection database, IConfiguration configuration) =>
        new(database.ConnectionString,
            configuration[DatabaseSetting] is { Length: > 0 } databaseName ? databaseName : "Memoria",
            configuration[ContainerSetting] is { Length: > 0 } containerName ? containerName : "Domain");

    /// <summary>
    /// Reads where the Cosmos store behind one named connection string keeps its documents:
    /// <c>Databases:{name}:Cosmos:DatabaseName</c> and <c>ContainerName</c>, with the string the
    /// tool has always read, <c>Memoria</c>, keeping its older unnamed settings as well.
    /// </summary>
    public static CosmosStore Of(string name, DatabaseConnection database, IConfiguration configuration)
    {
        var older = name == DatabaseConnection.Name ? Of(database, configuration) : null;

        return new CosmosStore(
            database.ConnectionString,
            configuration[$"Databases:{name}:Cosmos:DatabaseName"] is { Length: > 0 } databaseName
                ? databaseName
                : older?.DatabaseName ?? "Memoria",
            configuration[$"Databases:{name}:Cosmos:ContainerName"] is { Length: > 0 } containerName
                ? containerName
                : older?.ContainerName ?? "Domain");
    }
}

/// <summary>
/// The streamed event store as a Cosmos account carries it: one client, and the framework's own
/// SDK-based <see cref="IDomainService"/> over it.
/// </summary>
/// <remarks>
/// <para>
/// Shared rather than written twice. Both the web tool and the sample seeder reach the same Cosmos
/// store from the same connection string, and a second copy of this wiring is a second chance for
/// one of them to end up pointed at a container the other does not read. It is the same reason
/// <see cref="DatabaseConnection"/> is shared between them.
/// </para>
/// <para>
/// <c>AddMemoriaCosmos</c> is not used, because it builds a client of its own from an endpoint and a
/// key. What is given here is a connection string, and the client built from it is registered as a
/// singleton in its own right so that whatever else the host reads with — the tool's document
/// queries, for one — reads through that one client. The provider is handed it and told not to own
/// it, so shutdown disposes the client once, through the registration below.
/// </para>
/// <para>
/// Nothing here provisions anything: a database and a container are the host's business, and only
/// the seeder has any reason to create them.
/// </para>
/// </remarks>
public static class CosmosStoreRegistration
{
    /// <summary>
    /// Registers the client, and the streamed store's read and write path over it.
    /// </summary>
    /// <param name="services">The container.</param>
    /// <param name="store">The account, database and container to reach.</param>
    public static IServiceCollection AddCosmosStreamedStore(
        this IServiceCollection services, CosmosStore store)
    {
        services.AddSingleton(_ => new CosmosClient(store.ConnectionString, new CosmosClientOptions
        {
            // Questions are asked across streams, so most reads are cross-partition queries. Gateway
            // mode is the one that works wherever this is run, including from behind the proxies an
            // operator's machine tends to sit behind.
            ConnectionMode = ConnectionMode.Gateway
        }));

        // The write types' other two dependencies, which AddMemoriaCosmos would otherwise have
        // registered. Tried rather than added, because a host that already has them keeps its own.
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

        services.AddSingleton(provider => new CosmosClientProvider(
            provider.GetRequiredService<CosmosClient>(), store.DatabaseName, store.ContainerName));

        services.AddScoped<ICosmosDataStore, CosmosDataStore>();
        services.AddScoped<IDomainService, CosmosDomainService>();

        return services;
    }
}
