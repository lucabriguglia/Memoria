using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;
using Xunit;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests.Fixtures;

public sealed class SqlServerFixture : DatabaseFixture
{
    private const string Image = "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04";

    private readonly MsSqlContainer container = new MsSqlBuilder(Image).Build();

    protected override string EngineName => "SQL Server";

    protected override async Task<string> StartContainerAsync()
    {
        await container.StartAsync();
        return container.GetConnectionString();
    }

    protected override Task StopContainerAsync() => container.DisposeAsync().AsTask();

    /// <summary>Points the SQL Server tests at an existing server instead of a container.</summary>
    public const string Variable = "MEMORIA_TEST_SQLSERVER";

    public override string ServerVariable => Variable;

    protected override string WithDatabase(string database) =>
        new SqlConnectionStringBuilder(ConnectionString)
        {
            InitialCatalog = database,
            TrustServerCertificate = true
        }.ConnectionString;

    /// <remarks>
    /// <c>SINGLE_USER WITH ROLLBACK IMMEDIATE</c> ends any connection a test left open rather than
    /// failing on it. The pool is cleared first so this process's own idle connections go quietly.
    /// </remarks>
    protected override async Task DropDatabaseAsync(string database)
    {
        SqlConnection.ClearAllPools();

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            $"""
            IF DB_ID(N'{database}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{database}];
            END
            """, connection);
        await command.ExecuteNonQueryAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SQL Server";
}
