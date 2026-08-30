using FluentAssertions;
using FluentAssertions.Execution;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests.Fixtures;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Relational.Tests.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests;

[Trait("Category", "Container")]
[Collection(PostgreSqlCollection.Name)]
public class PostgreSqlSchemaTests(PostgreSqlFixture fixture)
{
    private async Task WithFreshSchema(Func<RelationalTestDbContext, Task> assert)
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);

        await using var dbContext = StoreSchema.OnPostgreSql(fixture.ConnectionStringForFreshDatabase());

        try
        {
            await dbContext.Database.EnsureCreatedAsync();
            await assert(dbContext);
        }
        finally
        {
            try
            {
                await dbContext.Database.EnsureDeletedAsync();
            }
            catch
            {
                // The container is discarded after the run; a failed cleanup must not mask the result.
            }
        }
    }

    [RequiresDockerFact]
    public async Task TheStoreSchemaCanBeCreated() => await WithFreshSchema(_ => Task.CompletedTask);

    [RequiresDockerFact]
    public async Task UnboundedStringKeysBecomeUnboundedText() =>
        await WithFreshSchema(async dbContext =>
        {
            var events = await ColumnMetadata.ReadAsync(dbContext, "events");

            using (new AssertionScope())
            {
                // Npgsql maps an unbounded string to text, which has no length limit, so there is no
                // provider-chosen key width to overflow.
                events["Id"].ToString().Should().Be("text");
                events["StreamId"].ToString().Should().Be("character varying(255)");
            }
        });
}
