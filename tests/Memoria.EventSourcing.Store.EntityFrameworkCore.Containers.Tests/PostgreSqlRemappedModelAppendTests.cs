using AwesomeAssertions;
using Memoria.EventSourcing.Dcb;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Relational.Tests;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Relational.Tests.Models;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Xunit;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests;

/// <summary>
/// The append routine is generated from the context's model, so the store's documented
/// customisations — payloads as <c>jsonb</c>, tables and columns mapped elsewhere — append in one
/// round trip too, and two contexts mapping different tables in one database each get their own.
/// </summary>
[Trait("Category", "Container")]
[Collection(PostgreSqlCollection.Name)]
public class PostgreSqlRemappedModelAppendTests(PostgreSqlFixture fixture)
{
    private static readonly Tag SeatA1 = new("seat", "a1");
    private static readonly SeatId SeatA1Aggregate = new("a1");

    private static TaggedEvent Reserved(string student) => new(new SeatReservedEvent("a1", student), [SeatA1]);

    /// <summary>Every table renamed into a schema of its own, and both tag columns renamed.</summary>
    public sealed class BoxOfficeDbContext(DbContextOptions<DcbDbContext> options)
        : TestDbContext(options, TimeProvider.System, new StubHttpContextAccessor())
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<DcbEventEntity>().ToTable("Events", "box_office");
            modelBuilder.Entity<DcbEventTagEntity>().ToTable("EventTags", "box_office")
                .Property(tag => tag.Tag).HasColumnName("tag_value");
            modelBuilder.Entity<DcbTagHeadEntity>().ToTable("TagHeads", "box_office")
                .Property(head => head.Tag).HasColumnName("tag_key");
            modelBuilder.Entity<DcbSnapshotEntity>().ToTable("Snapshots", "box_office");
        }
    }

    private static TContext Connect<TContext>(string connectionString, params IInterceptor[] interceptors)
        where TContext : DbContext
    {
        var options = new DbContextOptionsBuilder<DcbDbContext>().UseNpgsql(connectionString)
            .AddInterceptors(interceptors).Options;

        return typeof(TContext) == typeof(JsonbDcbDbContext)
            ? (TContext)(DbContext)new JsonbDcbDbContext(options, TimeProvider.System, new StubHttpContextAccessor())
            : typeof(TContext) == typeof(BoxOfficeDbContext)
                ? (TContext)(DbContext)new BoxOfficeDbContext(options)
                : (TContext)(DbContext)new TestDbContext(options, TimeProvider.System, new StubHttpContextAccessor());
    }

    private async Task WithDatabase(Func<string, Task> body)
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);

        TypeBindings.EventTypeBindings = new Dictionary<string, Type> { { "SeatReserved:1", typeof(SeatReservedEvent) } };
        DcbTypeBindings.AggregateTypeBindings = new Dictionary<string, Type> { { "Seat:1", typeof(SeatAggregate) } };

        await body(fixture.ConnectionStringForFreshDatabase());
        NpgsqlConnection.ClearAllPools();
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

    private static async Task<int> AppendAndSave<TContext>(string connectionString) where TContext : DbContext, IDcbDbContext
    {
        var commands = new CommandCounter();
        await using var dbContext = Connect<TContext>(connectionString, commands);

        var appended = await dbContext.SaveEvents([Reserved("s7")], AppendCondition.NothingAppendedFor(TagQuery.AnyOf(SeatA1)));
        appended.IsSuccess.Should().BeTrue(appended.Failure?.Description);

        var seat = (await dbContext.GetAggregate(SeatA1Aggregate, ReadMode.SnapshotWithNewEventsOrCreate)).Value!;
        seat.Reserve("a1", "s8");
        commands.Reset();
        var saved = await dbContext.SaveAggregate(SeatA1Aggregate, seat,
            new AppendCondition(SeatA1Aggregate.Boundary, seat.LatestPosition));
        saved.IsSuccess.Should().BeTrue(saved.Failure?.Description);

        return commands.Count;
    }

    [RequiresDockerFact]
    public Task A_jsonb_model_appends_and_saves_in_one_round_trip_each() =>
        WithDatabase(async connectionString =>
        {
            await Install<JsonbDcbDbContext>(connectionString, createDatabase: true);

            var commands = new CommandCounter();
            await using var dbContext = Connect<JsonbDcbDbContext>(connectionString, commands);
            var appended = await dbContext.SaveEvents([Reserved("s7")],
                AppendCondition.NothingAppendedFor(TagQuery.AnyOf(SeatA1)));

            appended.IsSuccess.Should().BeTrue(appended.Failure?.Description);
            commands.Count.Should().Be(1, $"the append should be one round trip, but sent:\n{commands}");
            (await AppendAndSaveAgain<JsonbDcbDbContext>(connectionString)).Should().Be(1);

            await using var reading = Connect<JsonbDcbDbContext>(connectionString);
            var payloadType = await reading.Database
                .SqlQueryRaw<string>("SELECT jsonb_typeof(\"Data\") AS \"Value\" FROM \"DcbSnapshots\"")
                .SingleAsync();
            payloadType.Should().Be("object", "the snapshot is stored as jsonb, not as a jsonb string");
            (await reading.GetAggregate(SeatA1Aggregate, ReadMode.SnapshotOnly)).Value!.ReservedBy.Should().Be("s8");
        });

    [RequiresDockerFact]
    public Task A_model_mapped_to_other_tables_columns_and_schema_appends_and_saves_in_one_round_trip_each() =>
        WithDatabase(async connectionString =>
        {
            await Install<BoxOfficeDbContext>(connectionString, createDatabase: true);

            var commands = new CommandCounter();
            await using var dbContext = Connect<BoxOfficeDbContext>(connectionString, commands);
            var appended = await dbContext.SaveEvents([Reserved("s7")],
                AppendCondition.NothingAppendedFor(TagQuery.AnyOf(SeatA1)));
            var refused = await dbContext.SaveEvents([Reserved("s9")],
                AppendCondition.NothingAppendedFor(TagQuery.AnyOf(SeatA1)));

            appended.IsSuccess.Should().BeTrue(appended.Failure?.Description);
            refused.Failure!.Type.Should().Be(EventSourcing.StoreFailures.ConcurrencyConflictType);
            commands.Count.Should().Be(2, $"each append should be one round trip, but sent:\n{commands}");
            (await AppendAndSaveAgain<BoxOfficeDbContext>(connectionString)).Should().Be(1);

            await using var reading = Connect<BoxOfficeDbContext>(connectionString);
            (await reading.GetEvents(TagQuery.AnyOf(SeatA1))).Should().HaveCount(2);
            (await reading.GetAggregate(SeatA1Aggregate, ReadMode.SnapshotOnly)).Value!.ReservedBy.Should().Be("s8");
        });

    [RequiresDockerFact]
    public Task Two_contexts_mapping_different_tables_in_one_database_each_have_their_own_routine() =>
        WithDatabase(async connectionString =>
        {
            await Install<TestDbContext>(connectionString, createDatabase: true);
            await Install<BoxOfficeDbContext>(connectionString, createDatabase: false);

            (await AppendAndSave<TestDbContext>(connectionString)).Should().Be(1);
            (await AppendAndSave<BoxOfficeDbContext>(connectionString)).Should().Be(1);

            await using var reading = Connect<TestDbContext>(connectionString);
            var routines = await reading.Database.SqlQueryRaw<string>(
                    """
                    SELECT n.nspname || '.' || p.proname AS "Value" FROM pg_proc p
                    JOIN pg_namespace n ON n.oid = p.pronamespace
                    WHERE p.proname LIKE 'memoria_dcb_append_v1%' ORDER BY 1
                    """)
                .ToListAsync();

            routines.Should().HaveCount(2, "one routine per mapping");
            routines.Should().Contain("public.memoria_dcb_append_v1", "the default model keeps the plain name");
            routines.Should().Contain(name => name.StartsWith("box_office.memoria_dcb_append_v1_"),
                "a remapped model's routine lives beside its events, under a name of its own");

            (await reading.DcbEvents.CountAsync()).Should().Be(2);
            await using var boxOffice = Connect<BoxOfficeDbContext>(connectionString);
            (await boxOffice.DcbEvents.CountAsync()).Should().Be(2);
        });

    /// <summary>Reads the aggregate, decides once more and saves it, counting only the save.</summary>
    private static async Task<int> AppendAndSaveAgain<TContext>(string connectionString)
        where TContext : DbContext, IDcbDbContext
    {
        var commands = new CommandCounter();
        await using var dbContext = Connect<TContext>(connectionString, commands);
        var seat = (await dbContext.GetAggregate(SeatA1Aggregate, ReadMode.SnapshotWithNewEventsOrCreate)).Value!;
        seat.Reserve("a1", "s8");

        commands.Reset();
        var saved = await dbContext.SaveAggregate(SeatA1Aggregate, seat,
            new AppendCondition(SeatA1Aggregate.Boundary, seat.LatestPosition));

        saved.IsSuccess.Should().BeTrue(saved.Failure?.Description);
        return commands.Count;
    }
}
