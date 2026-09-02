using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace Memoria.Benchmarks.Store;

/// <summary>The database the benchmarks run against.</summary>
public enum StoreEngine
{
    /// <summary>In-process, no dependencies, and round trips are almost free.</summary>
    Sqlite,

    /// <summary>A real engine in a container, where a round trip is a round trip.</summary>
    SqlServer
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

/// <summary>Applies the provider for a chosen engine to a context's options.</summary>
public static class StoreEngineExtensions
{
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

            default:
                throw new ArgumentOutOfRangeException(nameof(engine), engine, null);
        }
    }
}
