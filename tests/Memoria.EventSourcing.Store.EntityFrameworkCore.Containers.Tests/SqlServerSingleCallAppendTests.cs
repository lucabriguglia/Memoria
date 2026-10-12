using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Claims;
using AwesomeAssertions;
using Memoria.EventSourcing.Dcb;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Relational.Tests;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Relational.Tests.Models;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests;

/// <summary>
/// On SQL Server a guarded DCB append is one call to the store's append procedure, rather than the
/// reads, writes and commit the Entity Framework Core path sends.
/// </summary>
/// <remarks>
/// <para>
/// What an append decides is covered by <see cref="DcbStoreOnEngineTests"/>, which runs on SQL Server
/// through the procedure too. These cover what is particular to the procedure: how many round trips
/// it costs, the race it has instead of the one the Entity Framework Core path guards against, the
/// deadlocks the spike ran into, and the failure and audit behaviour it reaches by a different route.
/// </para>
/// <para>
/// Run twice, with READ_COMMITTED_SNAPSHOT off and on: the procedure's boundary check relies on each
/// statement reading afresh, which both settings give it in different ways.
/// </para>
/// </remarks>
public abstract class SqlServerSingleCallAppendTests(SqlServerFixture fixture)
{
    private static readonly Tag SeatA1 = new("seat", "a1");

    /// <summary>Whether the database the tests run against reads committed data from row versions.</summary>
    protected abstract bool ReadCommittedSnapshot { get; }

    private static TaggedEvent Reserved(string seat, string student, params Tag[] tags) =>
        new(new SeatReservedEvent(seat, student), tags.Length > 0 ? tags : [new Tag("seat", seat)]);

    /// <summary>A fresh database with the store's schema, and contexts onto it.</summary>
    private sealed class TestDatabase(string connectionString, List<TestDbContext> opened)
    {
        public string ConnectionString => connectionString;

        public TestDbContext Connect(params IInterceptor[] interceptors) =>
            Track(DcbStoreSchema.OnSqlServer(connectionString, interceptors));

        public TestDbContext Connect(TimeProvider timeProvider, IHttpContextAccessor httpContextAccessor) =>
            Track(new TestDbContext(new DbContextOptionsBuilder<DcbDbContext>().UseSqlServer(connectionString).Options,
                timeProvider, httpContextAccessor));

        private TestDbContext Track(TestDbContext context)
        {
            opened.Add(context);
            return context;
        }
    }

    /// <summary>
    /// Runs the body against a fresh database with the store's schema and, unless told otherwise, its
    /// append procedure.
    /// </summary>
    private async Task WithDatabase(Func<TestDatabase, Task> body, bool installRoutine = true)
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);

        TypeBindings.EventTypeBindings = new Dictionary<string, Type>
        {
            { "SeatReserved:1", typeof(SeatReservedEvent) },
            { "SeatReleased:1", typeof(SeatReleasedEvent) }
        };

        var opened = new List<TestDbContext>();
        var database = new TestDatabase(fixture.ConnectionStringForFreshDatabase(), opened);
        var setup = database.Connect();

        try
        {
            await setup.Database.EnsureCreatedAsync();

            // Set either way: EF Core creates SQL Server databases with it on.
            await setup.Database.ExecuteSqlRawAsync(
                $"ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT {(ReadCommittedSnapshot ? "ON" : "OFF")} WITH ROLLBACK IMMEDIATE");

            if (installRoutine)
            {
                await setup.Database.EnsureDcbAppendRoutineAsync();
            }

            await body(database);
        }
        finally
        {
            foreach (var context in opened)
            {
                await context.DisposeAsync();
            }

            SqlConnection.ClearAllPools();
        }
    }

    [RequiresDockerFact]
    public Task The_database_reads_committed_data_as_the_test_says() =>
        WithDatabase(async database =>
        {
            var setting = await database.Connect().Database.SqlQueryRaw<bool>(
                "SELECT is_read_committed_snapshot_on AS [Value] FROM sys.databases WHERE database_id = DB_ID()")
                .SingleAsync();

            setting.Should().Be(ReadCommittedSnapshot);
        });

    [RequiresDockerFact]
    public Task A_guarded_append_over_existing_tag_heads_is_one_database_command() =>
        WithDatabase(async database =>
        {
            var seeding = database.Connect();
            await seeding.SaveEvents([Reserved("a1", "s7")], condition: null);
            var position = await seeding.GetLatestPosition(TagQuery.AnyOf(SeatA1));

            var commands = new CommandCounter();
            var appending = database.Connect(commands);

            var result = await appending.SaveEvents([Reserved("a1", "s8")],
                new AppendCondition(TagQuery.AnyOf(SeatA1), position));

            result.IsSuccess.Should().BeTrue(result.Failure?.Description);
            commands.Count.Should().Be(1, $"the append should be one round trip, but sent:\n{commands}");
        });

    [RequiresDockerFact]
    public Task A_guarded_append_introducing_new_tags_is_one_database_command() =>
        WithDatabase(async database =>
        {
            var commands = new CommandCounter();
            var appending = database.Connect(commands);

            var result = await appending.SaveEvents([Reserved("a1", "s7")],
                AppendCondition.NothingAppendedFor(TagQuery.AnyOf(SeatA1)));

            result.IsSuccess.Should().BeTrue(result.Failure?.Description);
            commands.Count.Should().Be(1, $"the append should be one round trip, but sent:\n{commands}");
        });

    [RequiresDockerFact]
    public Task A_guarded_append_of_several_events_is_one_database_command_and_tags_each_event_with_its_own_tags() =>
        WithDatabase(async database =>
        {
            var seatA2 = new Tag("seat", "a2");
            var student = new Tag("student", "s7");
            var commands = new CommandCounter();

            var result = await database.Connect(commands).SaveEvents(
                [Reserved("a1", "s7", SeatA1, student), Reserved("a2", "s7", seatA2, student), Reserved("a1", "s8")],
                AppendCondition.NothingAppendedFor(TagQuery.AnyOf(SeatA1, seatA2)));

            result.IsSuccess.Should().BeTrue(result.Failure?.Description);
            commands.Count.Should().Be(1, $"the append should be one round trip, but sent:\n{commands}");

            var reading = database.Connect();
            var written = await reading.DcbEvents.AsNoTracking().OrderBy(e => e.Position)
                .Select(e => new { e.Position, e.Data }).ToListAsync();
            written.Select(e => e.Data).Should().Equal(
                DomainSerializer.Current.Serialize(new SeatReservedEvent("a1", "s7")),
                DomainSerializer.Current.Serialize(new SeatReservedEvent("a2", "s7")),
                DomainSerializer.Current.Serialize(new SeatReservedEvent("a1", "s8")));

            var tags = await reading.DcbEventTags.AsNoTracking().ToListAsync();
            tags.GroupBy(t => t.Position).ToDictionary(g => g.Key, g => g.Select(t => t.Tag).Order().ToArray())
                .Should().BeEquivalentTo(new Dictionary<long, string[]>
                {
                    [written[0].Position] = ["seat:a1", "student:s7"],
                    [written[1].Position] = ["seat:a2", "student:s7"],
                    [written[2].Position] = ["seat:a1"]
                });
        });

    [RequiresDockerFact]
    public Task A_refused_append_reports_both_positions_writes_nothing_and_records_the_conflict() =>
        WithDatabase(async database =>
        {
            var dbContext = database.Connect();
            await dbContext.SaveEvents([Reserved("a1", "s7")], condition: null);
            var latest = await dbContext.GetLatestPosition(TagQuery.AnyOf(SeatA1));

            using var activity = new Activity("append").Start();
            var result = await dbContext.SaveEvents([Reserved("a1", "s8")],
                AppendCondition.NothingAppendedFor(TagQuery.AnyOf(SeatA1)));

            result.IsNotSuccess.Should().BeTrue();
            result.Failure!.Type.Should().Be(EventSourcing.StoreFailures.ConcurrencyConflictType);
            result.Failure.Tags!["expectedPosition"].Should().Be(AppendCondition.NoEvents.ToString());
            result.Failure.Tags["latestPosition"].Should().Be(latest.ToString());

            (await database.Connect().DcbEvents.CountAsync()).Should().Be(1, "a refused append writes nothing");

            var conflict = activity.Events.Should().ContainSingle(e => e.Name == "Concurrency Conflict").Subject;
            conflict.Tags.Should().Contain(new KeyValuePair<string, object?>("latestPosition", latest));
        });

    [RequiresDockerFact]
    public Task An_append_waiting_on_a_tag_head_is_refused_when_the_holder_moves_its_boundary() =>
        WithDatabase(async database =>
        {
            // The race the procedure has instead of the one the token guard covers: it locks before
            // it checks, so an append that passed the early check can wait on a head while the holder
            // commits into its boundary. The check it makes with the locks held has to see that.
            var seeding = database.Connect();
            await seeding.SaveEvents([Reserved("a1", "s7")], condition: null);
            var position = await seeding.GetLatestPosition(TagQuery.AnyOf(SeatA1));

            var holder = database.Connect();
            await using var holding = await holder.Database.BeginTransactionAsync();
            await holder.Database.ExecuteSqlRawAsync(
                "UPDATE [DcbTagHeads] SET [Token] = NEWID() WHERE [Tag] = {0}", SeatA1.ToString());

            var waiting = database.Connect().SaveEvents([Reserved("a1", "s9")],
                new AppendCondition(TagQuery.AnyOf(SeatA1), position));

            await UntilAnAppendIsWaitingOnALock(database.ConnectionString);

            await holder.Database.ExecuteSqlRawAsync(
                """
                INSERT [DcbEvents] ([EventType], [Data], [CreatedDate]) VALUES ('SeatReserved:1', {1}, SYSDATETIMEOFFSET());
                INSERT [DcbEventTags] ([Position], [Tag]) VALUES (CAST(SCOPE_IDENTITY() AS bigint), {0});
                """, SeatA1.ToString(), DomainSerializer.Current.Serialize(new SeatReservedEvent("a1", "s8")));
            await holding.CommitAsync();

            var result = await waiting;

            result.IsNotSuccess.Should().BeTrue("the holder appended into the boundary while the append waited");
            result.Failure!.Type.Should().Be(EventSourcing.StoreFailures.ConcurrencyConflictType);
            result.Failure.Tags!["latestPosition"].Should().Be((position + 1).ToString());
        });

    /// <summary>Waits until some session in this test's database is blocked on a lock.</summary>
    private static async Task UntilAnAppendIsWaitingOnALock(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        var deadline = DateTime.UtcNow.AddSeconds(30);

        while (DateTime.UtcNow < deadline)
        {
            await using var command = new SqlCommand(
                """
                SELECT COUNT(*) FROM sys.dm_exec_requests
                WHERE database_id = DB_ID() AND wait_type LIKE 'LCK%' AND session_id <> @@SPID
                """, connection);

            if ((int)(await command.ExecuteScalarAsync())! > 0)
            {
                return;
            }

            await Task.Delay(20);
        }

        throw new TimeoutException("No append started waiting on a lock.");
    }

    [RequiresDockerFact]
    public Task Concurrent_appends_on_one_boundary_are_never_both_accepted_on_the_same_position() =>
        WithDatabase(async database =>
        {
            const int writers = 8;
            const int appendsEach = 25;
            var acceptedOn = new ConcurrentBag<long>();
            var boundary = TagQuery.AnyOf(SeatA1);

            await Task.WhenAll(Enumerable.Range(0, writers).Select(writer => Task.Run(async () =>
            {
                await using var dbContext = DcbStoreSchema.OnSqlServer(database.ConnectionString);

                for (var accepted = 0; accepted < appendsEach;)
                {
                    var seen = await dbContext.GetLatestPosition(boundary);
                    var result = await dbContext.SaveEvents([Reserved("a1", $"s{writer}-{accepted}")],
                        new AppendCondition(boundary, seen));

                    if (result.IsSuccess)
                    {
                        acceptedOn.Add(seen);
                        accepted++;
                    }
                    else
                    {
                        result.Failure!.Type.Should().Be(EventSourcing.StoreFailures.ConcurrencyConflictType,
                            result.Failure.Description);
                    }
                }
            })));

            acceptedOn.Should().OnlyHaveUniqueItems("no two appends may be accepted against the same position");
            (await database.Connect().DcbEvents.CountAsync()).Should().Be(writers * appendsEach);
        });

    [RequiresDockerFact]
    public Task Concurrent_appends_on_disjoint_boundaries_all_succeed() =>
        WithDatabase(async database =>
        {
            // The spike's deadlock: scans took update locks on other appends' heads and events, so
            // writers that shared nothing deadlocked each other. Every one of these must be accepted.
            const int writers = 16;
            const int appendsEach = 25;

            var failures = await AllAtOnce(writers, async (writer, dbContext) =>
            {
                var seat = new Tag("seat", $"d{writer:00}");
                var problems = new List<string>();

                for (var i = 0; i < appendsEach; i++)
                {
                    var seen = await dbContext.GetLatestPosition(TagQuery.AnyOf(seat));
                    var result = await dbContext.SaveEvents([Reserved($"d{writer:00}", $"s{i}", seat)],
                        new AppendCondition(TagQuery.AnyOf(seat), seen));

                    if (result.IsNotSuccess)
                    {
                        problems.Add($"{seat} #{i}: {result.Failure!.Type} {result.Failure.Description}");
                    }
                }

                return problems;
            }, database);

            failures.Should().BeEmpty("writers on disjoint boundaries never refuse, deadlock or fail each other");
            (await database.Connect().DcbEvents.CountAsync()).Should().Be(writers * appendsEach);
        });

    [RequiresDockerFact]
    public Task Concurrent_appends_each_introducing_a_new_tag_all_succeed() =>
        WithDatabase(async database =>
        {
            // The spike's other deadlock: a missing key under HOLDLOCK locks the gap around it, and
            // appends introducing neighbouring tags deadlocked on those gaps. Several rounds, each
            // of sixteen writers starting together on a tag nobody has written yet.
            const int writers = 16;
            const int rounds = 5;

            var failures = new List<string>();
            for (var round = 0; round < rounds; round++)
            {
                var thisRound = round;
                failures.AddRange(await AllAtOnce(writers, async (writer, dbContext) =>
                {
                    var seat = new Tag("seat", $"n{thisRound}-{writer:00}");
                    var result = await dbContext.SaveEvents([Reserved($"n{thisRound}-{writer:00}", "s1", seat)],
                        AppendCondition.NothingAppendedFor(TagQuery.AnyOf(seat)));

                    return result.IsSuccess ? [] : [$"{seat}: {result.Failure!.Type} {result.Failure.Description}"];
                }, database));
            }

            failures.Should().BeEmpty("appends introducing different tags share nothing");
            (await database.Connect().DcbTagHeads.CountAsync()).Should().Be(writers * rounds);
        });

    /// <summary>Runs one body per writer, each on its own context, all released at the same moment.</summary>
    private static async Task<List<string>> AllAtOnce(int writers,
        Func<int, TestDbContext, Task<List<string>>> body, TestDatabase database)
    {
        using var start = new Barrier(writers);

        var outcomes = await Task.WhenAll(Enumerable.Range(0, writers).Select(writer => Task.Run(async () =>
        {
            await using var dbContext = DcbStoreSchema.OnSqlServer(database.ConnectionString);
            await dbContext.Database.OpenConnectionAsync();
            start.SignalAndWait();

            return await body(writer, dbContext);
        })));

        return outcomes.SelectMany(problems => problems).ToList();
    }

    [RequiresDockerFact]
    public Task An_append_through_the_procedure_is_stamped_as_the_entity_framework_core_path_stamps_it() =>
        WithDatabase(async database =>
        {
            var now = new DateTimeOffset(2026, 10, 12, 9, 30, 0, TimeSpan.Zero);
            var user = new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "auditor")], "Test"))
                }
            };
            var dbContext = database.Connect(new FakeTimeProvider(now), user);

            // Conditioned goes through the procedure; unconditional still goes through Entity
            // Framework Core, so the second is the reference the first must match.
            await dbContext.SaveEvents([Reserved("a1", "s7")], AppendCondition.NothingAppendedFor(TagQuery.AnyOf(SeatA1)));
            await dbContext.SaveEvents([Reserved("a2", "s7")], condition: null);

            var stamps = await database.Connect().DcbEvents.AsNoTracking()
                .OrderBy(e => e.Position)
                .Select(e => new { e.CreatedDate, e.CreatedBy })
                .ToListAsync();

            stamps.Should().HaveCount(2);
            stamps[0].Should().BeEquivalentTo(stamps[1], "both paths stamp the same time and user");
            stamps[0].CreatedDate.Should().Be(now);
            stamps[0].CreatedBy.Should().Be("auditor");
        });

    [RequiresDockerFact]
    public Task An_append_without_the_procedure_installed_says_how_to_install_it() =>
        WithDatabase(async database =>
        {
            var result = await database.Connect().SaveEvents([Reserved("a1", "s7")],
                AppendCondition.NothingAppendedFor(TagQuery.AnyOf(SeatA1)));

            result.IsNotSuccess.Should().BeTrue();
            result.Failure!.Type.Should().Be(EventSourcing.StoreFailures.StorageFailureType);
            result.Failure.Description.Should().Contain("memoria_dcb_append_v1")
                .And.Contain("EnsureDcbAppendRoutineAsync")
                .And.Contain("migration")
                .And.Contain("install script");
        }, installRoutine: false);

    [RequiresDockerFact]
    public Task Installing_the_procedure_twice_is_harmless() =>
        WithDatabase(async database =>
        {
            var dbContext = database.Connect();

            await dbContext.Database.EnsureDcbAppendRoutineAsync();

            var result = await dbContext.SaveEvents([Reserved("a1", "s7")],
                AppendCondition.NothingAppendedFor(TagQuery.AnyOf(SeatA1)));
            result.IsSuccess.Should().BeTrue(result.Failure?.Description);
        });
}

[Trait("Category", "Container")]
[Collection(SqlServerCollection.Name)]
public class SqlServerSingleCallAppendUnderLockingReadCommittedTests(SqlServerFixture fixture)
    : SqlServerSingleCallAppendTests(fixture)
{
    protected override bool ReadCommittedSnapshot => false;
}

[Trait("Category", "Container")]
[Collection(SqlServerCollection.Name)]
public class SqlServerSingleCallAppendUnderReadCommittedSnapshotTests(SqlServerFixture fixture)
    : SqlServerSingleCallAppendTests(fixture)
{
    protected override bool ReadCommittedSnapshot => true;
}
