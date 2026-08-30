using FluentAssertions;
using FluentAssertions.Execution;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests.Fixtures;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Relational.Tests.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests;

/// <summary>
/// Runs the shipped PostgreSQL migration script against a real engine, from a database put back into
/// the pre-1.5.0 index shape. Without this the script would only ever be checked by reading it.
/// </summary>
[Trait("Category", "Container")]
[Collection(PostgreSqlCollection.Name)]
public class PostgreSqlMigrationScriptTests(PostgreSqlFixture fixture)
{
    private const string ScriptFileName = "1.5.0-indexes-postgresql.sql";

    /// <summary>
    /// Puts a freshly created database back into the index shape 1.4.x produced, so the script has
    /// something real to migrate.
    /// </summary>
    private const string RevertToPreMigrationShape =
        """
        DROP INDEX public."IX_Events_StreamId_Sequence";
        CREATE INDEX "IX_Events_StreamId_Sequence" ON public."events" ("StreamId", "Sequence");
        CREATE INDEX "IX_Events_StreamId" ON public."events" ("StreamId");
        DROP INDEX public."IX_Events_StreamId_CreatedDate";
        """;

    private async Task WithPreMigrationDatabase(Func<RelationalTestDbContext, Task> act)
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);

        await using var dbContext = StoreSchema.OnPostgreSql(fixture.ConnectionStringForFreshDatabase());

        try
        {
            await dbContext.Database.EnsureCreatedAsync();
            await MigrationScript.ExecuteAsync(dbContext, RevertToPreMigrationShape);
            await act(dbContext);
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
    public async Task TheRevertedDatabaseHasThePreMigrationIndexes() =>
        await WithPreMigrationDatabase(async dbContext =>
        {
            var events = await IndexMetadata.ReadPostgreSqlAsync(dbContext, "events");

            using (new AssertionScope())
            {
                events.Should().Equal(
                    "IX_Events_EventType",
                    "IX_Events_StreamId",
                    "IX_Events_StreamId_Sequence");
            }
        });

    [RequiresDockerFact]
    public async Task TheScriptProducesTheIndexesTheModelDeclares() =>
        await WithPreMigrationDatabase(async dbContext =>
        {
            await MigrationScript.ExecuteAsync(dbContext, MigrationScript.Read(ScriptFileName));

            var events = await IndexMetadata.ReadPostgreSqlAsync(dbContext, "events");

            using (new AssertionScope())
            {
                events.Should().Equal(
                    "IX_Events_EventType",
                    "IX_Events_StreamId_CreatedDate",
                    "IX_Events_StreamId_Sequence unique");
            }
        });

    [RequiresDockerFact]
    public async Task TheScriptIsIdempotent() =>
        await WithPreMigrationDatabase(async dbContext =>
        {
            var script = MigrationScript.Read(ScriptFileName);

            await MigrationScript.ExecuteAsync(dbContext, script);
            var afterFirstRun = await IndexMetadata.ReadPostgreSqlAsync(dbContext, "events");

            var runAgain = async () => await MigrationScript.ExecuteAsync(dbContext, script);

            await runAgain.Should().NotThrowAsync();

            var afterSecondRun = await IndexMetadata.ReadPostgreSqlAsync(dbContext, "events");
            afterSecondRun.Should().Equal(afterFirstRun);
        });
}
