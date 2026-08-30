using FluentAssertions;
using FluentAssertions.Execution;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests.Fixtures;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Relational.Tests.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests;

[Trait("Category", "Container")]
[Collection(SqlServerCollection.Name)]
public class SqlServerSchemaTests(SqlServerFixture fixture)
{
    private async Task WithFreshSchema(Func<RelationalTestDbContext, Task> assert)
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);

        await using var dbContext = StoreSchema.OnSqlServer(fixture.ConnectionStringForFreshDatabase());

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
    public async Task AStreamIdBeyondTheColumnWidthIsRejected() =>
        await WithFreshSchema(async dbContext =>
        {
            // Backs the 255-character limit published in the Identifier lengths reference. The event
            // id stays inside its own 450-character column, so only StreamId overflows.
            var streamId = new string('s', 300);

            dbContext.Events.Add(new EventEntity
            {
                Id = $"{streamId}:1",
                StreamId = streamId,
                EventType = "TestAggregateCreated:1",
                Sequence = 1,
                Data = "{}"
            });

            var save = async () => await dbContext.SaveChangesAsync();

            var thrown = await save.Should().ThrowAsync<DbUpdateException>();

            thrown.And.InnerException.Should().NotBeNull();
            thrown.And.InnerException!.Message.Should().Contain("truncated");
        });

    [RequiresDockerFact]
    public async Task UnboundedStringKeysBecomeTheProviderDefaultWidth() =>
        await WithFreshSchema(async dbContext =>
        {
            var events = await ColumnMetadata.ReadAsync(dbContext, "events");

            using (new AssertionScope())
            {
                // EventEntity.Id has no MaxLength, so SQL Server falls back to its default width for
                // a string key: nvarchar(450), i.e. 900 bytes — exactly the clustered index limit,
                // and PK_events spans this column alone.
                events["Id"].ToString().Should().Be("nvarchar(450)");
                events["StreamId"].ToString().Should().Be("nvarchar(255)");
            }
        });
}
