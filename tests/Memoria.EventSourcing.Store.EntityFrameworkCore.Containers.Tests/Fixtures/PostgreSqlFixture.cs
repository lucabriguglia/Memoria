using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests.Fixtures;

public sealed class PostgreSqlFixture : DatabaseFixture
{
    private const string Image = "postgres:15.1";

    private readonly PostgreSqlContainer container = new PostgreSqlBuilder(Image).Build();

    protected override string EngineName => "PostgreSQL";

    protected override async Task<string> StartContainerAsync()
    {
        await container.StartAsync();
        return container.GetConnectionString();
    }

    protected override Task StopContainerAsync() => container.DisposeAsync().AsTask();

    /// <summary>Points the PostgreSQL tests at an existing server instead of a container.</summary>
    public const string Variable = "MEMORIA_TEST_POSTGRES";

    public override string ServerVariable => Variable;

    protected override string WithDatabase(string database) =>
        new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Database = database
        }.ConnectionString;

    /// <remarks>
    /// <c>WITH (FORCE)</c> (PostgreSQL 13+) ends any connection a test left open rather than failing
    /// on it. The pool is cleared first so this process's own idle connections go quietly.
    /// </remarks>
    protected override async Task DropDatabaseAsync(string database)
    {
        NpgsqlConnection.ClearAllPools();

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
    public const string Name = "PostgreSQL";
}
