using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Npgsql.Tests;

/// <summary>
/// xUnit collection fixture that starts a Postgres container for the whole test run.
/// Requires Docker to be running locally. If Docker is unavailable, the test class will
/// fail loudly during initialisation rather than silently passing.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    internal string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        TestTypeBindings.Register();

        await using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    internal TestDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new TestDbContext(options);
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = nameof(PostgresCollection);
}
