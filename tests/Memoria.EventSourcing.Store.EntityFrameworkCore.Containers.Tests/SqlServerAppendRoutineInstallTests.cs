using AwesomeAssertions;
using Memoria.EventSourcing.Dcb;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Relational.Tests;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Relational.Tests.Models;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests;

/// <summary>
/// The append procedure reaches a SQL Server database the three ways its tables do: an EF Core
/// migration, the install script, or <c>EnsureDcbAppendRoutineAsync</c> after <c>EnsureCreated</c> —
/// and all three install the same procedure.
/// </summary>
/// <remarks>
/// A migration is run the way <c>dotnet ef database update</c> runs one: its operations are turned
/// into SQL by the provider's migrations SQL generator against the model, then executed. The model
/// is the context's design-time model, which carries the same relational mapping — collations
/// included — a migration's target model does.
/// </remarks>
[Trait("Category", "Container")]
[Collection(SqlServerCollection.Name)]
public class SqlServerAppendRoutineInstallTests(SqlServerFixture fixture)
{
    private const string ScriptFileName = "2.1.0-install-dcb-sqlserver.sql";

    private static readonly Tag SeatA1 = new("seat", "a1");

    private static TestDbContext Default(string connectionString, params Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor[] interceptors) =>
        DcbStoreSchema.OnSqlServer(connectionString, interceptors);

    private static PostgreSqlRemappedModelAppendTests.BoxOfficeDbContext BoxOffice(string connectionString,
        params Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor[] interceptors) =>
        new(new DbContextOptionsBuilder<DcbDbContext>().UseSqlServer(connectionString).AddInterceptors(interceptors).Options);

    private async Task WithDatabase(Func<string, Task> body)
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);
        TypeBindings.EventTypeBindings = new Dictionary<string, Type>
        {
            { "SeatReserved:1", typeof(SeatReservedEvent) },
            { "SeatReleased:1", typeof(SeatReleasedEvent) }
        };

        await body(fixture.ConnectionStringForFreshDatabase());
        SqlConnection.ClearAllPools();
    }

    /// <summary>Runs migration operations against the context's database, as a migration would.</summary>
    private static async Task Migrate(DbContext dbContext, Action<MigrationBuilder, IModel> operations)
    {
        var model = dbContext.GetService<IDesignTimeModel>().Model;
        var migrationBuilder = new MigrationBuilder(dbContext.Database.ProviderName);
        operations(migrationBuilder, model);

        var commands = dbContext.GetService<IMigrationsSqlGenerator>().Generate(migrationBuilder.Operations, model);
        await dbContext.GetService<IMigrationCommandExecutor>()
            .ExecuteNonQueryAsync(commands, dbContext.GetService<IRelationalConnection>());
    }

    private static async Task<int> CommandsForOneAppend<TContext>(TContext dbContext, CommandCounter commands)
        where TContext : DbContext, IDcbDbContext
    {
        var result = await dbContext.SaveEvents([new TaggedEvent(new SeatReservedEvent("a1", "s7"), [SeatA1])],
            AppendCondition.NothingAppendedFor(TagQuery.AnyOf(SeatA1)));

        result.IsSuccess.Should().BeTrue(result.Failure?.Description);
        return commands.Count;
    }

    /// <summary>Every append procedure in the database, as schema.name.</summary>
    internal static Task<List<string>> Procedures(DbContext dbContext) =>
        dbContext.Database.SqlQueryRaw<string>(
                """
                SELECT s.[name] + '.' + p.[name] AS [Value] FROM sys.procedures p
                JOIN sys.schemas s ON s.[schema_id] = p.[schema_id]
                WHERE p.[name] LIKE 'memoria[_]dcb[_]append[_]v1%' ORDER BY 1
                """)
            .ToListAsync();

    private static Task<string> DefaultProcedureDefinition(DbContext dbContext) =>
        dbContext.Database.SqlQueryRaw<string>(
                "SELECT OBJECT_DEFINITION(OBJECT_ID(N'[dbo].[memoria_dcb_append_v1]')) AS [Value]")
            .SingleAsync();

    [RequiresDockerFact]
    public Task A_migration_installs_the_procedure_for_the_default_model() =>
        WithDatabase(async connectionString =>
        {
            await using (var setup = Default(connectionString))
            {
                await setup.Database.EnsureCreatedAsync();
                await Migrate(setup, (migration, model) => migration.CreateDcbAppendRoutine(model));
            }

            var commands = new CommandCounter();
            await using var dbContext = Default(connectionString, commands);
            (await CommandsForOneAppend(dbContext, commands)).Should().Be(1);
        });

    [RequiresDockerFact]
    public Task A_migration_installs_the_procedure_for_a_remapped_model() =>
        WithDatabase(async connectionString =>
        {
            await using (var setup = BoxOffice(connectionString))
            {
                await setup.Database.EnsureCreatedAsync();
                await Migrate(setup, (migration, model) => migration.CreateDcbAppendRoutine(model));
            }

            var commands = new CommandCounter();
            await using var dbContext = BoxOffice(connectionString, commands);
            (await CommandsForOneAppend(dbContext, commands)).Should().Be(1);
        });

    [RequiresDockerFact]
    public Task Dropping_the_procedure_in_a_migration_removes_only_that_mapping_s_procedure() =>
        WithDatabase(async connectionString =>
        {
            await using var defaultContext = Default(connectionString);
            await defaultContext.Database.EnsureCreatedAsync();
            await defaultContext.Database.EnsureDcbAppendRoutineAsync();

            await using var boxOffice = BoxOffice(connectionString);
            await boxOffice.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
            await Migrate(boxOffice, (migration, model) => migration.CreateDcbAppendRoutine(model));
            (await Procedures(defaultContext)).Should().HaveCount(2);

            await Migrate(boxOffice, (migration, model) => migration.DropDcbAppendRoutine(model));

            (await Procedures(defaultContext)).Should().Equal("dbo.memoria_dcb_append_v1");
        });

    [RequiresDockerFact]
    public Task The_script_a_migration_and_EnsureDcbAppendRoutineAsync_install_the_same_procedure() =>
        WithDatabase(async _ =>
        {
            await using var fromScript = Default(fixture.ConnectionStringForFreshDatabase());
            await fromScript.Database.EnsureCreatedAsync();
            await MigrationScript.ExecuteAsync(fromScript, MigrationScript.Read(ScriptFileName, "install"));

            await using var fromMigration = Default(fixture.ConnectionStringForFreshDatabase());
            await fromMigration.Database.EnsureCreatedAsync();
            await Migrate(fromMigration, (migration, model) => migration.CreateDcbAppendRoutine(model));

            await using var fromEnsure = Default(fixture.ConnectionStringForFreshDatabase());
            await fromEnsure.Database.EnsureCreatedAsync();
            await fromEnsure.Database.EnsureDcbAppendRoutineAsync();

            // The script's line endings are whatever the checkout gave it, and EF Core's SQL Server
            // migrations generator re-ends a SQL operation's lines with the running machine's
            // newline, and ends the batch with one, when it splits it into batches; what each
            // installs is compared, not how its lines end.
            var expected = await DefaultProcedureDefinition(fromEnsure);
            expected.Should().NotBeNullOrEmpty();
            (await DefaultProcedureDefinition(fromScript)).ReplaceLineEndings("\n").Should().Be(expected,
                "the install script must not drift from the procedure the store generates");
            (await DefaultProcedureDefinition(fromMigration)).ReplaceLineEndings("\n").TrimEnd().Should().Be(expected);
        });
}
