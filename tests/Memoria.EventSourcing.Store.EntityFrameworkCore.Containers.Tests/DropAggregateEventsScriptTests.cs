using FluentAssertions;
using FluentAssertions.Execution;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests.Fixtures;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Relational.Tests.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests;

/// <summary>
/// Rehearses the upgrade a consumer actually performs: a database standing at the 1.5.0 schema, with
/// <c>DomainAggregateEvents</c> present, has the 1.7.0 drop script applied to it.
/// </summary>
/// <remarks>
/// The 1.5.0 install script is the fixture here precisely because the current model can no longer
/// create that table. Without it there would be nothing to drop, and the script would be shipped
/// having only ever run against a database where it was a no-op.
/// </remarks>
[Trait("Category", "Container")]
public class DropAggregateEventsScriptTests
{
    private const string LinkTable = "DomainAggregateEvents";

    [Collection(SqlServerCollection.Name)]
    public class OnSqlServer(SqlServerFixture fixture)
    {
        [RequiresDockerFact]
        public async Task GivenA150Database_ThenTheScriptDropsOnlyTheLinkTable()
        {
            Assert.True(fixture.IsAvailable, fixture.UnavailableReason);

            await using var dbContext = StoreSchema.OnSqlServer(fixture.ConnectionStringForFreshDatabase());

            try
            {
                await dbContext.Database.EnsureCreatedAsync();
                await MigrationScript.ExecuteAsync(dbContext,
                    string.Join("\n", InstallScriptComparison.TablesInDropOrder
                        .Select(table => $"DROP TABLE [dbo].[{table}];")));
                await MigrationScript.ExecuteAsync(dbContext,
                    MigrationScript.Read("1.5.0-install-sqlserver.sql", "install"));

                (await ColumnMetadata.ReadAsync(dbContext, LinkTable)).Should()
                    .NotBeEmpty("the 1.5.0 schema has the link table — otherwise there is nothing to drop");

                await MigrationScript.ExecuteAsync(dbContext,
                    MigrationScript.Read("1.7.0-drop-aggregate-events-sqlserver.sql"));

                await AssertOnlyTheLinkTableWentAsync(dbContext);
            }
            finally
            {
                await Discard(dbContext);
            }
        }

        [RequiresDockerFact]
        public async Task GivenTheTableIsAlreadyGone_ThenTheScriptIsStillSafeToRun()
        {
            Assert.True(fixture.IsAvailable, fixture.UnavailableReason);

            await using var dbContext = StoreSchema.OnSqlServer(fixture.ConnectionStringForFreshDatabase());

            try
            {
                // The 1.7.0 model never creates the table, so this is the second-run case.
                await dbContext.Database.EnsureCreatedAsync();

                var script = MigrationScript.Read("1.7.0-drop-aggregate-events-sqlserver.sql");
                var run = async () => await MigrationScript.ExecuteAsync(dbContext, script);

                await run.Should().NotThrowAsync();
                await run.Should().NotThrowAsync("the script is documented as safe to run more than once");
            }
            finally
            {
                await Discard(dbContext);
            }
        }
    }

    [Collection(PostgreSqlCollection.Name)]
    public class OnPostgreSql(PostgreSqlFixture fixture)
    {
        [RequiresDockerFact]
        public async Task GivenA150Database_ThenTheScriptDropsOnlyTheLinkTable()
        {
            Assert.True(fixture.IsAvailable, fixture.UnavailableReason);

            await using var dbContext = StoreSchema.OnPostgreSql(fixture.ConnectionStringForFreshDatabase());

            try
            {
                await dbContext.Database.EnsureCreatedAsync();
                await MigrationScript.ExecuteAsync(dbContext,
                    string.Join("\n", InstallScriptComparison.TablesInDropOrder
                        .Select(table => $"DROP TABLE public.\"{table}\";")));
                await MigrationScript.ExecuteAsync(dbContext,
                    MigrationScript.Read("1.5.0-install-postgresql.sql", "install"));

                (await ColumnMetadata.ReadAsync(dbContext, LinkTable)).Should()
                    .NotBeEmpty("the 1.5.0 schema has the link table — otherwise there is nothing to drop");

                await MigrationScript.ExecuteAsync(dbContext,
                    MigrationScript.Read("1.7.0-drop-aggregate-events-postgresql.sql"));

                await AssertOnlyTheLinkTableWentAsync(dbContext);
            }
            finally
            {
                await Discard(dbContext);
            }
        }

        [RequiresDockerFact]
        public async Task GivenTheTableIsAlreadyGone_ThenTheScriptIsStillSafeToRun()
        {
            Assert.True(fixture.IsAvailable, fixture.UnavailableReason);

            await using var dbContext = StoreSchema.OnPostgreSql(fixture.ConnectionStringForFreshDatabase());

            try
            {
                await dbContext.Database.EnsureCreatedAsync();

                var script = MigrationScript.Read("1.7.0-drop-aggregate-events-postgresql.sql");
                var run = async () => await MigrationScript.ExecuteAsync(dbContext, script);

                await run.Should().NotThrowAsync();
                await run.Should().NotThrowAsync("the script is documented as safe to run more than once");
            }
            finally
            {
                await Discard(dbContext);
            }
        }
    }

    /// <summary>
    /// The link table is gone and every table the store still uses survived — a drop script that
    /// took a neighbour with it would be far worse than one that did nothing.
    /// </summary>
    private static async Task AssertOnlyTheLinkTableWentAsync(RelationalTestDbContext dbContext)
    {
        using (new AssertionScope())
        {
            (await ColumnMetadata.ReadAsync(dbContext, LinkTable)).Should().BeEmpty();

            foreach (var table in InstallScriptComparison.TablesInDropOrder)
            {
                (await ColumnMetadata.ReadAsync(dbContext, table)).Should()
                    .NotBeEmpty($"{table} must survive the drop");
            }
        }
    }

    private static async Task Discard(RelationalTestDbContext dbContext)
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
