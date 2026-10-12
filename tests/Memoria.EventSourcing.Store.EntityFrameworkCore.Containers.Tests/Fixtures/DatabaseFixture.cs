using System.Collections.Concurrent;
using Xunit;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests.Fixtures;

/// <summary>
/// Starts a database engine in a container for the tests that need a real provider. SQLite cannot
/// answer questions about column widths, index key limits or engine-specific DDL, so those claims
/// have to be executed against the engines the store actually targets.
/// </summary>
/// <remarks>
/// <para>
/// A missing or unhealthy Docker daemon is reported as a skip rather than a failure, so the suite
/// stays runnable on machines without Docker. It does mean a green run is not proof these tests
/// executed — check for skips when the answer matters.
/// </para>
/// <para>
/// When <see cref="ServerVariable"/> holds a connection string, the fixture uses that server instead
/// of starting a container, for machines where Docker will not run. A container takes everything
/// in it when it goes; a server does not, so the fixture remembers every database it hands out and
/// drops them when it is disposed — and only those, so it never touches anything else on the server,
/// another run's databases included.
/// </para>
/// </remarks>
public abstract class DatabaseFixture : IAsyncLifetime
{
    private readonly ConcurrentBag<string> handedOut = [];
    private bool onExistingServer;

    /// <summary>Connection string for the server's default database.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>Null when the engine is available; otherwise why the tests cannot run.</summary>
    public string? UnavailableReason { get; private set; }

    public bool IsAvailable => UnavailableReason is null;

    protected abstract string EngineName { get; }

    /// <summary>The variable that points this engine at an existing server instead of a container.</summary>
    public abstract string ServerVariable { get; }

    protected abstract Task<string> StartContainerAsync();

    protected abstract Task StopContainerAsync();

    /// <summary>This fixture's connection string, pointed at another database on the same server.</summary>
    protected abstract string WithDatabase(string database);

    /// <summary>Drops a database on the server, closing any connections still open to it.</summary>
    protected abstract Task DropDatabaseAsync(string database);

    /// <summary>
    /// A connection string pointing at a uniquely named database, so each test can create the store's
    /// schema from scratch without colliding with another test.
    /// </summary>
    public string ConnectionStringForFreshDatabase()
    {
        var database = FreshDatabaseName();

        if (onExistingServer)
        {
            handedOut.Add(database);
        }

        return WithDatabase(database);
    }

    private static string FreshDatabaseName() => $"memoria_{Guid.NewGuid():N}";

    public async Task InitializeAsync()
    {
        var existingServer = Environment.GetEnvironmentVariable(ServerVariable);
        if (!string.IsNullOrWhiteSpace(existingServer))
        {
            ConnectionString = existingServer;
            onExistingServer = true;
            return;
        }

        try
        {
            ConnectionString = await StartContainerAsync();
        }
        catch (Exception exception)
        {
            UnavailableReason =
                $"{EngineName} container could not be started ({exception.GetType().Name}: {exception.Message}). " +
                $"Is Docker running? Or set {ServerVariable} to the connection string of an existing {EngineName} server.";
        }
    }

    public async Task DisposeAsync()
    {
        if (onExistingServer)
        {
            await DropHandedOutDatabasesAsync();
            return;
        }

        try
        {
            await StopContainerAsync();
        }
        catch
        {
            // Nothing useful to do while tearing down, and throwing here would mask test results.
        }
    }

    /// <summary>
    /// Drops every database this fixture handed out, trying them all before reporting any that could
    /// not be dropped. Unlike a container that fails to stop, a database left behind on someone's
    /// server is worth a visible failure.
    /// </summary>
    private async Task DropHandedOutDatabasesAsync()
    {
        var failures = new List<Exception>();

        foreach (var database in handedOut)
        {
            try
            {
                await DropDatabaseAsync(database);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        if (failures.Count > 0)
        {
            throw new AggregateException(
                $"{failures.Count} {EngineName} test database(s) could not be dropped from {ServerVariable}'s server.",
                failures);
        }
    }
}
