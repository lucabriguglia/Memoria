using AwesomeAssertions;
using Memoria.EventSourcing.Dcb;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Relational.Tests;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Relational.Tests.Models;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Xunit;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests;

/// <summary>
/// The append routine reaches a database the three ways its tables do: an EF Core migration, the
/// install script, or <c>EnsureDcbAppendRoutineAsync</c> after <c>EnsureCreated</c> — and all three
/// install the same routine.
/// </summary>
/// <remarks>
/// A migration is run the way <c>dotnet ef database update</c> runs one: its operations are turned
/// into SQL by the provider's migrations SQL generator against the model, then executed. The model
/// is the context's design-time model, which carries the same relational mapping a migration's
/// target model does.
/// </remarks>
[Trait("Category", "Container")]
[Collection(PostgreSqlCollection.Name)]
public class PostgreSqlAppendRoutineInstallTests(PostgreSqlFixture fixture)
{
    private const string ScriptFileName = "2.1.0-install-dcb-postgresql.sql";

    private static readonly Tag SeatA1 = new("seat", "a1");

    private static TestDbContext Default(string connectionString, params Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor[] interceptors) =>
        DcbStoreSchema.OnPostgreSql(connectionString, interceptors);

    private static PostgreSqlRemappedModelAppendTests.BoxOfficeDbContext BoxOffice(string connectionString,
        params Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor[] interceptors) =>
        new(new DbContextOptionsBuilder<DcbDbContext>().UseNpgsql(connectionString).AddInterceptors(interceptors).Options);

    private async Task WithDatabase(Func<string, Task> body)
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);
        TypeBindings.EventTypeBindings = new Dictionary<string, Type>
        {
            { "SeatReserved:1", typeof(SeatReservedEvent) },
            { "SeatReleased:1", typeof(SeatReleasedEvent) }
        };

        await body(fixture.ConnectionStringForFreshDatabase());
        NpgsqlConnection.ClearAllPools();
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

    private static Task<List<string>> Routines(DbContext dbContext) =>
        dbContext.Database.SqlQueryRaw<string>(
                """
                SELECT n.nspname || '.' || p.proname AS "Value" FROM pg_proc p
                JOIN pg_namespace n ON n.oid = p.pronamespace
                WHERE p.proname LIKE 'memoria_dcb_append_v1%' ORDER BY 1
                """)
            .ToListAsync();

    private static Task<string> DefaultRoutineDefinition(DbContext dbContext) =>
        dbContext.Database.SqlQueryRaw<string>(
                """
                SELECT pg_get_functiondef(p.oid) AS "Value" FROM pg_proc p
                JOIN pg_namespace n ON n.oid = p.pronamespace
                WHERE n.nspname = 'public' AND p.proname = 'memoria_dcb_append_v1'
                """)
            .SingleAsync();

    [RequiresDockerFact]
    public Task A_migration_installs_the_routine_for_the_default_model() =>
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
    public Task A_migration_installs_the_routine_for_a_remapped_model() =>
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
    public Task Dropping_the_routine_in_a_migration_removes_only_that_mapping_s_routine() =>
        WithDatabase(async connectionString =>
        {
            await using var defaultContext = Default(connectionString);
            await defaultContext.Database.EnsureCreatedAsync();
            await defaultContext.Database.EnsureDcbAppendRoutineAsync();

            await using var boxOffice = BoxOffice(connectionString);
            await boxOffice.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
            await Migrate(boxOffice, (migration, model) => migration.CreateDcbAppendRoutine(model));
            (await Routines(defaultContext)).Should().HaveCount(2);

            await Migrate(boxOffice, (migration, model) => migration.DropDcbAppendRoutine(model));

            (await Routines(defaultContext)).Should().Equal("public.memoria_dcb_append_v1");
        });

    [RequiresDockerFact]
    public Task The_script_a_migration_and_EnsureDcbAppendRoutineAsync_install_the_same_routine() =>
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

            // The script's line endings are whatever the checkout gave it; what it installs is
            // compared, not how its lines end.
            var expected = await DefaultRoutineDefinition(fromEnsure);
            (await DefaultRoutineDefinition(fromScript)).ReplaceLineEndings("\n").Should().Be(expected,
                "the install script must not drift from the routine the store generates");
            (await DefaultRoutineDefinition(fromMigration)).Should().Be(expected);
        });

    [RequiresDockerFact]
    public Task The_generated_routine_ends_its_lines_the_same_whatever_machine_built_the_store() =>
        WithDatabase(async connectionString =>
        {
            // A raw string literal takes the line endings of its source file, so a store built from a
            // Windows checkout would install a routine that differs from one built on Linux.
            await using var dbContext = Default(connectionString);
            await dbContext.Database.EnsureCreatedAsync();
            await dbContext.Database.EnsureDcbAppendRoutineAsync();

            (await DefaultRoutineDefinition(dbContext)).Should().NotContain("\r");
        });
}

/// <summary>
/// The migration helpers on an engine whose appends go through Entity Framework Core: nothing to
/// install, so nothing added to the migration.
/// </summary>
public class AppendRoutineMigrationOnOtherEnginesTests
{
    [Fact]
    public void The_migration_helpers_add_nothing_on_an_engine_without_a_routine()
    {
        var model = DcbStoreSchema.OnPostgreSql("Host=unused").GetService<IDesignTimeModel>().Model;
        var migrationBuilder = new MigrationBuilder("Microsoft.EntityFrameworkCore.Sqlite");

        migrationBuilder.CreateDcbAppendRoutine(model);
        migrationBuilder.DropDcbAppendRoutine(model);

        migrationBuilder.Operations.Should().BeEmpty();
    }
}
