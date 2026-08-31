using FluentAssertions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Relational.Tests;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests;

/// <summary>
/// Proves the shipped DCB install scripts stand up the same schema the model declares, so a consumer
/// who runs one gets a database the store actually works against.
/// </summary>
[Trait("Category", "Container")]
[Collection(SqlServerCollection.Name)]
public class SqlServerDcbInstallScriptTests(SqlServerFixture fixture)
{
    private const string ScriptFileName = "1.8.0-install-dcb-sqlserver.sql";

    private static Task<IReadOnlyList<string>> Describe(DbContext dbContext) =>
        DcbInstallScriptComparison.DescribeAsync(dbContext,
            (context, table) => IndexMetadata.ReadSqlServerAsync(context, table, includePrimaryKeys: true));

    private static string DropEveryTable() =>
        string.Join("\n", DcbInstallScriptComparison.TablesInDropOrder
            .Select(table => $"DROP TABLE [dbo].[{table}];"));

    private async Task<TestDbContext> EmptyDatabase()
    {
        var dbContext = DcbStoreSchema.OnSqlServer(fixture.ConnectionStringForFreshDatabase());
        await dbContext.Database.EnsureCreatedAsync();
        await MigrationScript.ExecuteAsync(dbContext, DropEveryTable());
        return dbContext;
    }

    private static async Task Discard(TestDbContext dbContext)
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

        var fromModel = DcbStoreSchema.OnSqlServer(fixture.ConnectionStringForFreshDatabase());
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

    [RequiresDockerFact]
    public async Task TheScriptedTagColumnsAreCaseSensitive()
    {
        // The schema comparison would pass if both the model and the script were wrong together.
        // This asserts the value itself, because it is a correctness property rather than a match.
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);

        var dbContext = await EmptyDatabase();

        try
        {
            await MigrationScript.ExecuteAsync(dbContext, MigrationScript.Read(ScriptFileName, "install"));

            foreach (var table in new[] { "DcbEventTags", "DcbTagHeads" })
            {
                var collations = await DcbCollationMetadata.ReadAsync(dbContext, table);

                collations["Tag"].Should().EndWith("_CS_AS",
                    $"a case-insensitive {table}.Tag would silently merge seat:A1 into seat:a1");
            }
        }
        finally
        {
            await Discard(dbContext);
        }
    }
}

[Trait("Category", "Container")]
[Collection(PostgreSqlCollection.Name)]
public class PostgreSqlDcbInstallScriptTests(PostgreSqlFixture fixture)
{
    private const string ScriptFileName = "1.8.0-install-dcb-postgresql.sql";

    private static Task<IReadOnlyList<string>> Describe(DbContext dbContext) =>
        DcbInstallScriptComparison.DescribeAsync(dbContext,
            (context, table) => IndexMetadata.ReadPostgreSqlAsync(context, table, includePrimaryKeys: true));

    private static string DropEveryTable() =>
        string.Join("\n", DcbInstallScriptComparison.TablesInDropOrder
            .Select(table => $"DROP TABLE public.\"{table}\";"));

    private async Task<TestDbContext> EmptyDatabase()
    {
        var dbContext = DcbStoreSchema.OnPostgreSql(fixture.ConnectionStringForFreshDatabase());
        await dbContext.Database.EnsureCreatedAsync();
        await MigrationScript.ExecuteAsync(dbContext, DropEveryTable());
        return dbContext;
    }

    private static async Task Discard(TestDbContext dbContext)
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

        var fromModel = DcbStoreSchema.OnPostgreSql(fixture.ConnectionStringForFreshDatabase());
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

    [RequiresDockerFact]
    public async Task TheScriptedTagColumnsAreByteOrdered()
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);

        var dbContext = await EmptyDatabase();

        try
        {
            await MigrationScript.ExecuteAsync(dbContext, MigrationScript.Read(ScriptFileName, "install"));

            foreach (var table in new[] { "DcbEventTags", "DcbTagHeads" })
            {
                var collations = await DcbCollationMetadata.ReadAsync(dbContext, table);

                collations["Tag"].Should().Be("C",
                    $"a linguistic collation on {table}.Tag would not compare tags the way .NET does");
            }
        }
        finally
        {
            await Discard(dbContext);
        }
    }
}
