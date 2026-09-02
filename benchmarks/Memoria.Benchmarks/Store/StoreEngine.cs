using Microsoft.Data.SqlClient;
using Memoria.EventSourcing;
using Memoria.EventSourcing.Store.Cosmos;
using Memoria.EventSourcing.Store.Cosmos.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace Memoria.Benchmarks.Store;

/// <summary>The database the benchmarks run against.</summary>
public enum StoreEngine
{
    /// <summary>In-process, no dependencies, and round trips are almost free.</summary>
    Sqlite,

    /// <summary>A real engine in a container, where a round trip is a round trip.</summary>
    SqlServer,

    /// <summary>The other engine the store targets, also in a container.</summary>
    PostgreSql,

    /// <summary>
    /// The streamed store only. Cosmos DB cannot host DCB: an append has to condition on a tag
    /// query and write atomically, and a transactional batch is scoped to one logical partition
    /// while a boundary is not. See docs/concepts/providers.md.
    /// </summary>
    Cosmos
}

/// <summary>
/// The SQL Server container the benchmarks share, started once per process.
/// </summary>
/// <remarks>
/// <para>
/// One container for the whole run: starting it costs far more than any benchmark it hosts, and
/// BenchmarkDotNet already runs each job in its own process, so process-wide is exactly one per job.
/// Each harness still gets freshly named databases inside it, because <c>EnsureCreated</c> does
/// nothing on a database that already has tables.
/// </para>
/// <para>
/// It is deliberately not disposed. The process ends when the job ends, Testcontainers' Ryuk reaps
/// the container after it, and a cleanup hook racing BenchmarkDotNet's teardown would only add a way
/// for a run to fail after producing its results.
/// </para>
/// </remarks>
public static class SqlServerContainer
{
    private const string Image = "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04";

    private static readonly Lazy<string> Started = new(Start, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The connection string for the container's default database.</summary>
    public static string ConnectionString => Started.Value;

    /// <summary>A connection string for a uniquely named database that does not exist yet.</summary>
    public static string ForFreshDatabase(string name) =>
        new SqlConnectionStringBuilder(ConnectionString)
        {
            InitialCatalog = $"bench_{name}_{Guid.NewGuid():N}",
            TrustServerCertificate = true
        }.ConnectionString;

    private static string Start()
    {
        var container = new MsSqlBuilder(Image).Build();

        try
        {
            container.StartAsync().GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"The SQL Server container could not be started ({exception.GetType().Name}: {exception.Message}). " +
                "Is Docker running? Run with --filter on a Sqlite-only benchmark to skip it.", exception);
        }

        return container.GetConnectionString();
    }
}


/// <summary>
/// The PostgreSQL container the benchmarks share, started once per process, on the same terms as
/// <see cref="SqlServerContainer"/>.
/// </summary>
/// <remarks>
/// The DCB store pins the tag columns to the <c>C</c> collation here, chosen by provider name in
/// <c>DcbDbContext</c>, so nothing engine-specific is configured at this end. Event data is the
/// default <c>text</c> rather than the <c>jsonb</c> override, which keeps the payload identical to
/// the other two engines and the comparison about the store.
/// </remarks>
public static class PostgreSqlContainer
{
    private const string Image = "postgres:15.1";

    private static readonly Lazy<string> Started = new(Start, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The connection string for the container's default database.</summary>
    public static string ConnectionString => Started.Value;

    /// <summary>A connection string for a uniquely named database that does not exist yet.</summary>
    public static string ForFreshDatabase(string name) =>
        new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Database = $"bench_{name}_{Guid.NewGuid():N}"
        }.ConnectionString;

    private static string Start()
    {
        var container = new Testcontainers.PostgreSql.PostgreSqlBuilder(Image).Build();

        try
        {
            container.StartAsync().GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"The PostgreSQL container could not be started ({exception.GetType().Name}: {exception.Message}). " +
                "Is Docker running? Filter to a Sqlite-only run to skip it.", exception);
        }

        return container.GetConnectionString();
    }
}


/// <summary>
/// The Cosmos DB emulator the benchmarks use, and the streamed store built on it.
/// </summary>
/// <remarks>
/// <para>
/// The local emulator on <c>https://localhost:8081</c> with the well-known key, matching what the
/// Cosmos test project does. There is no container image here for the same reason there is no CI job
/// for those tests: the emulator is a local-only gate. Start it before running the Cosmos cases.
/// </para>
/// <para>
/// A database per harness, so a run never reads another run's documents.
/// </para>
/// </remarks>
public static class CosmosEmulator
{
    public const string Endpoint = "https://localhost:8081";

    private const string WellKnownKey =
        "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";

    /// <summary>
    /// Creates a domain service over a freshly named database, creating the database and container.
    /// </summary>
    public static IDomainService CreateDomainService(IHttpContextAccessor httpContextAccessor)
    {
        var options = Options.Create(new CosmosOptions
        {
            Endpoint = Endpoint,
            AuthKey = WellKnownKey,
            DatabaseName = $"bench_{Guid.NewGuid():N}"
        });

        var clientProvider = new CosmosClientProvider(options);

        try
        {
            new CosmosSetup(options, clientProvider).CreateDatabaseAndContainerIfNotExist()
                .GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"The Cosmos DB emulator could not be reached at {Endpoint} " +
                $"({exception.GetType().Name}: {exception.Message}). Start it, or filter the Cosmos " +
                "cases out of the run.", exception);
        }

        var dataStore = new CosmosDataStore(clientProvider, TimeProvider.System, httpContextAccessor);

        return new CosmosDomainService(clientProvider, TimeProvider.System, httpContextAccessor, dataStore);
    }
}

/// <summary>Applies the provider for a chosen engine to a context's options.</summary>
public static class StoreEngineExtensions
{
    /// <summary>Whether a DCB store exists for this engine.</summary>
    public static bool SupportsDcb(this StoreEngine engine) => engine is not StoreEngine.Cosmos;
    public static DbContextOptionsBuilder<T> UseEngine<T>(this DbContextOptionsBuilder<T> builder,
        StoreEngine engine, string name, out IDisposable? owned) where T : DbContext
    {
        switch (engine)
        {
            case StoreEngine.Sqlite:
                var connection = new Microsoft.Data.Sqlite.SqliteConnection(
                    $"DataSource={name}-{Guid.NewGuid()};Mode=Memory;Cache=Shared");
                connection.Open();
                owned = connection;
                return builder.UseSqlite(connection);

            case StoreEngine.SqlServer:
                owned = null;
                return builder.UseSqlServer(SqlServerContainer.ForFreshDatabase(name));

            case StoreEngine.PostgreSql:
                owned = null;
                return builder.UseNpgsql(PostgreSqlContainer.ForFreshDatabase(name));

            default:
                throw new ArgumentOutOfRangeException(nameof(engine), engine, null);
        }
    }
}
