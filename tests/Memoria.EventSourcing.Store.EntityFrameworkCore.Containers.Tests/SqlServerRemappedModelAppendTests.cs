using AwesomeAssertions;
using Memoria.EventSourcing.Dcb;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Relational.Tests;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Relational.Tests.Models;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests;

/// <summary>
/// The SQL Server procedure is generated from the context's model, so a context that maps the
/// store's tables elsewhere gets a procedure that writes where it maps them, under a name of its own.
/// </summary>
[Trait("Category", "Container")]
[Collection(SqlServerCollection.Name)]
public class SqlServerRemappedModelAppendTests(SqlServerFixture fixture)
{
    private static readonly Tag SeatA1 = new("seat", "a1");
    private static readonly SeatId SeatA1Aggregate = new("a1");

    private static TaggedEvent Reserved(string student) => new(new SeatReservedEvent("a1", student), [SeatA1]);

    private static TContext Connect<TContext>(string connectionString, params IInterceptor[] interceptors)
        where TContext : DbContext
    {
        var options = new DbContextOptionsBuilder<DcbDbContext>().UseSqlServer(connectionString)
            .AddInterceptors(interceptors).Options;

        return typeof(TContext) == typeof(PostgreSqlRemappedModelAppendTests.BoxOfficeDbContext)
            ? (TContext)(DbContext)new PostgreSqlRemappedModelAppendTests.BoxOfficeDbContext(options)
            : typeof(TContext) == typeof(LedgerDbContext)
                ? (TContext)(DbContext)new LedgerDbContext(options)
                : (TContext)(DbContext)new TestDbContext(options, TimeProvider.System, new StubHttpContextAccessor());
    }

    private async Task WithDatabase(Func<string, Task> body)
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);

        TypeBindings.EventTypeBindings = new Dictionary<string, Type>
        {
            { "SeatReserved:1", typeof(SeatReservedEvent) },
            { "SeatReleased:1", typeof(SeatReleasedEvent) }
        };
        DcbTypeBindings.AggregateTypeBindings = new Dictionary<string, Type> { { "Seat:1", typeof(SeatAggregate) } };

        await body(fixture.ConnectionStringForFreshDatabase());
        SqlConnection.ClearAllPools();
    }

    private static async Task Install<TContext>(string connectionString, bool createDatabase) where TContext : DbContext
    {
        await using var setup = Connect<TContext>(connectionString);

        if (createDatabase)
        {
            await setup.Database.EnsureCreatedAsync();
        }
        else
        {
            // EnsureCreated does nothing on a database that already has tables, so the second
            // context's tables are created directly.
            await setup.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
        }

        await setup.Database.EnsureDcbAppendRoutineAsync();
    }

    /// <summary>Appends once and saves the aggregate once, returning the commands each took.</summary>
    private static async Task<(int Append, int Save)> AppendAndSave<TContext>(string connectionString)
        where TContext : DbContext, IDcbDbContext
    {
        var commands = new CommandCounter();
        await using var dbContext = Connect<TContext>(connectionString, commands);

        var appended = await dbContext.SaveEvents([Reserved("s7")], AppendCondition.NothingAppendedFor(TagQuery.AnyOf(SeatA1)));
        appended.IsSuccess.Should().BeTrue(appended.Failure?.Description);
        var append = commands.Count;

        var seat = (await dbContext.GetAggregate(SeatA1Aggregate, ReadMode.SnapshotWithNewEventsOrCreate)).Value!;
        seat.Reserve("a1", "s8");
        commands.Reset();
        var saved = await dbContext.SaveAggregate(SeatA1Aggregate, seat,
            new AppendCondition(SeatA1Aggregate.Boundary, seat.LatestPosition));
        saved.IsSuccess.Should().BeTrue(saved.Failure?.Description);

        return (append, commands.Count);
    }

    [RequiresDockerFact]
    public Task A_model_mapped_to_other_tables_columns_and_schema_appends_and_saves_in_one_round_trip_each() =>
        WithDatabase(async connectionString =>
        {
            await Install<PostgreSqlRemappedModelAppendTests.BoxOfficeDbContext>(connectionString, createDatabase: true);

            (await AppendAndSave<PostgreSqlRemappedModelAppendTests.BoxOfficeDbContext>(connectionString))
                .Should().Be((1, 1));

            await using var reading = Connect<PostgreSqlRemappedModelAppendTests.BoxOfficeDbContext>(connectionString);
            var refused = await reading.SaveEvents([Reserved("s9")], AppendCondition.NothingAppendedFor(TagQuery.AnyOf(SeatA1)));
            refused.Failure!.Type.Should().Be(EventSourcing.StoreFailures.ConcurrencyConflictType);
            (await reading.GetEvents(TagQuery.AnyOf(SeatA1))).Should().HaveCount(2);
            (await reading.GetAggregate(SeatA1Aggregate, ReadMode.SnapshotOnly)).Value!.ReservedBy.Should().Be("s8");
        });

    /// <summary>The store's tables spread over two schemas: the events in one, the rest in another.</summary>
    public sealed class LedgerDbContext(DbContextOptions<DcbDbContext> options)
        : TestDbContext(options, TimeProvider.System, new StubHttpContextAccessor())
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<DcbEventEntity>().ToTable("Events", "ledger");
            modelBuilder.Entity<DcbEventTagEntity>().ToTable("EventTags", "boundaries");
            modelBuilder.Entity<DcbTagHeadEntity>().ToTable("TagHeads", "boundaries");
            modelBuilder.Entity<DcbSnapshotEntity>().ToTable("Snapshots", "boundaries");
        }
    }

    [RequiresDockerFact]
    public Task A_model_with_its_tables_in_different_schemas_appends_and_saves_in_one_round_trip_each() =>
        WithDatabase(async connectionString =>
        {
            // The procedure lives in the events table's schema, where unqualified names would also
            // resolve; the other tables are only found if the procedure names their schema.
            await Install<LedgerDbContext>(connectionString, createDatabase: true);

            (await AppendAndSave<LedgerDbContext>(connectionString)).Should().Be((1, 1));

            await using var reading = Connect<LedgerDbContext>(connectionString);
            (await reading.GetAggregate(SeatA1Aggregate, ReadMode.SnapshotOnly)).Value!.ReservedBy.Should().Be("s8");
        });

    [RequiresDockerFact]
    public Task Two_contexts_mapping_different_tables_in_one_database_each_have_their_own_procedure() =>
        WithDatabase(async connectionString =>
        {
            await Install<TestDbContext>(connectionString, createDatabase: true);
            await Install<PostgreSqlRemappedModelAppendTests.BoxOfficeDbContext>(connectionString, createDatabase: false);

            (await AppendAndSave<TestDbContext>(connectionString)).Should().Be((1, 1));
            (await AppendAndSave<PostgreSqlRemappedModelAppendTests.BoxOfficeDbContext>(connectionString)).Should().Be((1, 1));

            await using var reading = Connect<TestDbContext>(connectionString);
            var procedures = await SqlServerAppendRoutineInstallTests.Procedures(reading);

            procedures.Should().HaveCount(2, "one procedure per mapping");
            procedures.Should().Contain("dbo.memoria_dcb_append_v1", "the default model keeps the plain name");
            procedures.Should().Contain(name => name.StartsWith("box_office.memoria_dcb_append_v1_"),
                "a remapped model's procedure lives beside its events, under a name of its own");

            (await reading.DcbEvents.CountAsync()).Should().Be(2);
            await using var boxOffice = Connect<PostgreSqlRemappedModelAppendTests.BoxOfficeDbContext>(connectionString);
            (await boxOffice.DcbEvents.CountAsync()).Should().Be(2);
        });
}
