using AwesomeAssertions;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests.Fixtures;
using Microsoft.Data.SqlClient;
using Npgsql;
using Xunit;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests;

/// <summary>
/// Whether the container suite can run, and where: Docker, or a server named by
/// <c>MEMORIA_TEST_POSTGRES</c> / <c>MEMORIA_TEST_SQLSERVER</c>.
/// </summary>
/// <remarks>
/// Pure: the environment and the filesystem are passed in, so these run anywhere.
/// </remarks>
public class TestServerAvailabilityTests
{
    private static readonly string[] DockerPipes = [@"\\.\pipe\docker_engine"];

    [Fact]
    public void Without_docker_or_a_configured_server_the_suite_is_skipped_and_says_how_to_run_it()
    {
        var reason = TestServerAvailability.UnavailableReason(_ => null, _ => false, isWindows: true);

        reason.Should().NotBeNull();
        reason.Should().Contain("MEMORIA_TEST_POSTGRES").And.Contain("MEMORIA_TEST_SQLSERVER");
    }

    [Theory]
    [InlineData("MEMORIA_TEST_POSTGRES")]
    [InlineData("MEMORIA_TEST_SQLSERVER")]
    public void A_configured_server_lets_the_suite_run_without_docker(string variable)
    {
        var reason = TestServerAvailability.UnavailableReason(
            name => name == variable ? "Server=somewhere" : null, _ => false, isWindows: true);

        reason.Should().BeNull();
    }

    [Fact]
    public void A_blank_variable_is_not_a_configured_server()
    {
        var reason = TestServerAvailability.UnavailableReason(
            name => name == "MEMORIA_TEST_POSTGRES" ? "  " : null, _ => false, isWindows: true);

        reason.Should().NotBeNull();
    }

    [Fact]
    public void A_docker_pipe_lets_the_suite_run_on_windows()
    {
        var reason = TestServerAvailability.UnavailableReason(_ => null, path => DockerPipes.Contains(path), isWindows: true);

        reason.Should().BeNull();
    }

    [Fact]
    public void A_docker_socket_lets_the_suite_run_elsewhere()
    {
        var reason = TestServerAvailability.UnavailableReason(
            _ => null, path => path == "/var/run/docker.sock", isWindows: false);

        reason.Should().BeNull();
    }

    [Fact]
    public void Docker_host_is_taken_at_its_word()
    {
        var reason = TestServerAvailability.UnavailableReason(
            name => name == "DOCKER_HOST" ? "tcp://elsewhere:2375" : null, _ => false, isWindows: true);

        reason.Should().BeNull();
    }
}

/// <summary>
/// A fixture that cannot start its engine says which variable would point it at an existing server,
/// so a run with only one engine configured fails with a way forward rather than a bare Docker error.
/// </summary>
public class FixtureUnavailableTests
{
    private sealed class ContainerlessFixture : DatabaseFixture
    {
        protected override string EngineName => "Test engine";

        public override string ServerVariable => "MEMORIA_TEST_NOWHERE";

        protected override Task<string> StartContainerAsync() =>
            throw new InvalidOperationException("Docker is not running.");

        protected override Task StopContainerAsync() => Task.CompletedTask;

        protected override string WithDatabase(string database) => database;

        protected override Task DropDatabaseAsync(string database) => Task.CompletedTask;
    }

    [Fact]
    public async Task A_fixture_that_cannot_start_its_container_names_the_variable_to_set()
    {
        var fixture = new ContainerlessFixture();

        await fixture.InitializeAsync();

        fixture.IsAvailable.Should().BeFalse();
        fixture.UnavailableReason.Should().Contain("MEMORIA_TEST_NOWHERE");
    }
}

/// <summary>
/// Fixtures pointed at an existing server use it, and leave it as they found it.
/// </summary>
/// <remarks>
/// <para>
/// Each runs only when its engine's variable is set; with containers there is nothing to clean up,
/// because the container goes with everything in it.
/// </para>
/// <para>
/// A connection is left open on each handed-out database, as a test that forgot to dispose one
/// would: the drop has to get past it rather than fail on it.
/// </para>
/// </remarks>
[Trait("Category", "Container")]
public class ExistingServerTests
{
    private const string PostgresVariable = "MEMORIA_TEST_POSTGRES";
    private const string SqlServerVariable = "MEMORIA_TEST_SQLSERVER";

    [RequiresServerFact(PostgresVariable)]
    public async Task A_postgresql_fixture_on_an_existing_server_drops_the_databases_it_handed_out()
    {
        var server = Environment.GetEnvironmentVariable(PostgresVariable)!;
        var fixture = new PostgreSqlFixture();
        await fixture.InitializeAsync();
        fixture.IsAvailable.Should().BeTrue(fixture.UnavailableReason);
        new NpgsqlConnectionStringBuilder(fixture.ConnectionString).Host
            .Should().Be(new NpgsqlConnectionStringBuilder(server).Host, "the fixture should use the configured server");

        var handedOut = new NpgsqlConnectionStringBuilder(fixture.ConnectionStringForFreshDatabase()).Database!;
        await PostgreSql(server, $"CREATE DATABASE \"{handedOut}\"");
        await using var leftOpen = new NpgsqlConnection(
            new NpgsqlConnectionStringBuilder(server) { Database = handedOut }.ConnectionString);
        await leftOpen.OpenAsync();

        await fixture.DisposeAsync();

        (await PostgreSqlDatabaseExists(server, handedOut)).Should().BeFalse();
    }

    [RequiresServerFact(PostgresVariable)]
    public async Task A_postgresql_fixture_leaves_databases_it_did_not_hand_out()
    {
        var server = Environment.GetEnvironmentVariable(PostgresVariable)!;
        var someoneElses = $"memoria_{Guid.NewGuid():N}";
        await PostgreSql(server, $"CREATE DATABASE \"{someoneElses}\"");

        try
        {
            var fixture = new PostgreSqlFixture();
            await fixture.InitializeAsync();
            fixture.ConnectionStringForFreshDatabase();
            await fixture.DisposeAsync();

            (await PostgreSqlDatabaseExists(server, someoneElses)).Should().BeTrue();
        }
        finally
        {
            await PostgreSql(server, $"DROP DATABASE IF EXISTS \"{someoneElses}\"");
        }
    }

    [RequiresServerFact(SqlServerVariable)]
    public async Task A_sql_server_fixture_on_an_existing_server_drops_the_databases_it_handed_out()
    {
        var server = Environment.GetEnvironmentVariable(SqlServerVariable)!;
        var fixture = new SqlServerFixture();
        await fixture.InitializeAsync();
        fixture.IsAvailable.Should().BeTrue(fixture.UnavailableReason);
        new SqlConnectionStringBuilder(fixture.ConnectionString).DataSource
            .Should().Be(new SqlConnectionStringBuilder(server).DataSource, "the fixture should use the configured server");

        var handedOut = new SqlConnectionStringBuilder(fixture.ConnectionStringForFreshDatabase()).InitialCatalog;
        await SqlServer(server, $"CREATE DATABASE [{handedOut}]");
        await using var leftOpen = new SqlConnection(
            new SqlConnectionStringBuilder(server) { InitialCatalog = handedOut }.ConnectionString);
        await leftOpen.OpenAsync();

        await fixture.DisposeAsync();

        (await SqlServerDatabaseExists(server, handedOut)).Should().BeFalse();
    }

    [RequiresServerFact(SqlServerVariable)]
    public async Task A_sql_server_fixture_leaves_databases_it_did_not_hand_out()
    {
        var server = Environment.GetEnvironmentVariable(SqlServerVariable)!;
        var someoneElses = $"memoria_{Guid.NewGuid():N}";
        await SqlServer(server, $"CREATE DATABASE [{someoneElses}]");

        try
        {
            var fixture = new SqlServerFixture();
            await fixture.InitializeAsync();
            fixture.ConnectionStringForFreshDatabase();
            await fixture.DisposeAsync();

            (await SqlServerDatabaseExists(server, someoneElses)).Should().BeTrue();
        }
        finally
        {
            await SqlServer(server, $"IF DB_ID(N'{someoneElses}') IS NOT NULL DROP DATABASE [{someoneElses}]");
        }
    }

    private static async Task PostgreSql(string server, string sql)
    {
        await using var connection = new NpgsqlConnection(server);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<bool> PostgreSqlDatabaseExists(string server, string database)
    {
        await using var connection = new NpgsqlConnection(server);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname = @name", connection);
        command.Parameters.AddWithValue("name", database);
        return (long)(await command.ExecuteScalarAsync())! > 0;
    }

    private static async Task SqlServer(string server, string sql)
    {
        await using var connection = new SqlConnection(server);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<bool> SqlServerDatabaseExists(string server, string database)
    {
        await using var connection = new SqlConnection(server);
        await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT COUNT(*) FROM sys.databases WHERE name = @name", connection);
        command.Parameters.AddWithValue("@name", database);
        return (int)(await command.ExecuteScalarAsync())! > 0;
    }
}
