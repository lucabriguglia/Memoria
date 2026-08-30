using FluentAssertions;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests.Fixtures;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Relational.Tests.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests;

/// <summary>
/// Proves the shipped SQL Server install script stands up the same schema the model declares, so a
/// consumer who runs it gets a database the store actually works against.
/// </summary>
[Trait("Category", "Container")]
[Collection(SqlServerCollection.Name)]
public class SqlServerInstallScriptTests(SqlServerFixture fixture)
{
    private const string ScriptFileName = "1.7.0-install-sqlserver.sql";

    private static Task<IReadOnlyList<string>> Describe(RelationalTestDbContext dbContext) =>
        InstallScriptComparison.DescribeAsync(dbContext,
            (context, table) => IndexMetadata.ReadSqlServerAsync(context, table, includePrimaryKeys: true));

    private static string DropEveryTable() =>
        string.Join("\n", InstallScriptComparison.TablesInDropOrder
            .Select(table => $"DROP TABLE [dbo].[{table}];"));

    private async Task<RelationalTestDbContext> EmptyDatabase()
    {
        var dbContext = StoreSchema.OnSqlServer(fixture.ConnectionStringForFreshDatabase());
        await dbContext.Database.EnsureCreatedAsync();
        await MigrationScript.ExecuteAsync(dbContext, DropEveryTable());
        return dbContext;
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

        await dbContext.DisposeAsync();
    }

    [RequiresDockerFact]
    public async Task TheInstallScriptProducesTheSchemaTheModelDeclares()
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);

        var fromModel = StoreSchema.OnSqlServer(fixture.ConnectionStringForFreshDatabase());
        var fromScript = await EmptyDatabase();

        try
        {
            await fromModel.Database.EnsureCreatedAsync();
            await MigrationScript.ExecuteAsync(fromScript, MigrationScript.Read(ScriptFileName, "install"));

            var expected = await Describe(fromModel);
            var actual = await Describe(fromScript);

            expected.Should().NotBeEmpty("the comparison would pass vacuously against an empty schema");
            actual.Should().Equal(expected);
        }
        finally
        {
            await Discard(fromModel);
            await Discard(fromScript);
        }
    }

    [RequiresDockerFact]
    public async Task TheInstallScriptIsIdempotent()
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);

        var dbContext = await EmptyDatabase();

        try
        {
            var script = MigrationScript.Read(ScriptFileName, "install");

            await MigrationScript.ExecuteAsync(dbContext, script);
            var afterFirstRun = await Describe(dbContext);

            var runAgain = async () => await MigrationScript.ExecuteAsync(dbContext, script);

            await runAgain.Should().NotThrowAsync();
            (await Describe(dbContext)).Should().Equal(afterFirstRun);
        }
        finally
        {
            await Discard(dbContext);
        }
    }
}
