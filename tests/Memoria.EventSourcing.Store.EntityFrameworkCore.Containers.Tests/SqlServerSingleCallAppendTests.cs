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

        DcbTypeBindings.AggregateTypeBindings = new Dictionary<string, Type>
        {
            { "Seat:1", typeof(SeatAggregate) }
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
                try
                {
                    await context.DisposeAsync();
                }
                catch
                {
                    // A context a failed test left mid-command cannot be disposed cleanly; cleanup
                    // must not mask the result.
                }
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
            await using var throughEntityFrameworkCore =
                InterfaceOnlyDcbDbContext.OnSqlServer(database.ConnectionString, new FakeTimeProvider(now), user);

            // The first goes through the procedure; the second through Entity Framework Core, the
            // reference the first must match.
            await dbContext.SaveEvents([Reserved("a1", "s7")], AppendCondition.NothingAppendedFor(TagQuery.AnyOf(SeatA1)));
            await throughEntityFrameworkCore.SaveEvents([Reserved("a2", "s7")], condition: null);

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

    private static readonly SeatId SeatA1Aggregate = new("a1");

    private static async Task<SeatAggregate> StoredSeat(TestDbContext dbContext) =>
        (await dbContext.GetAggregate(SeatA1Aggregate, ReadMode.SnapshotOnly)).Value!;

    [RequiresDockerFact]
    public Task A_guarded_aggregate_save_writing_its_first_snapshot_is_one_database_command() =>
        WithDatabase(async database =>
        {
            var seat = new SeatAggregate();
            seat.Reserve("a1", "s7");

            var commands = new CommandCounter();
            var result = await database.Connect(commands).SaveAggregate(SeatA1Aggregate, seat,
                AppendCondition.NothingAppendedFor(SeatA1Aggregate.Boundary));

            result.IsSuccess.Should().BeTrue(result.Failure?.Description);
            commands.Count.Should().Be(1, $"the save should be one round trip, but sent:\n{commands}");
        });

    [RequiresDockerFact]
    public Task A_guarded_aggregate_save_replacing_its_snapshot_is_one_database_command() =>
        WithDatabase(async database =>
        {
            var first = new SeatAggregate();
            first.Reserve("a1", "s7");
            await database.Connect().SaveAggregate(SeatA1Aggregate, first,
                AppendCondition.NothingAppendedFor(SeatA1Aggregate.Boundary));

            var seat = await StoredSeat(database.Connect());
            seat.Reserve("a1", "s8");

            var commands = new CommandCounter();
            var result = await database.Connect(commands).SaveAggregate(SeatA1Aggregate, seat,
                new AppendCondition(SeatA1Aggregate.Boundary, seat.LatestPosition));

            result.IsSuccess.Should().BeTrue(result.Failure?.Description);
            commands.Count.Should().Be(1, $"the save should be one round trip, but sent:\n{commands}");
            (await StoredSeat(database.Connect())).ReservedBy.Should().Be("s8");
        });

    [RequiresDockerFact]
    public Task A_saved_aggregate_reads_back_from_its_snapshot_at_the_position_it_appended() =>
        WithDatabase(async database =>
        {
            var seat = new SeatAggregate();
            seat.Reserve("a1", "s7");
            seat.Reserve("a1", "s8");

            var result = await database.Connect().SaveAggregate(SeatA1Aggregate, seat,
                AppendCondition.NothingAppendedFor(SeatA1Aggregate.Boundary));

            result.IsSuccess.Should().BeTrue(result.Failure?.Description);
            var appendedTo = await database.Connect().GetLatestPosition(SeatA1Aggregate.Boundary);
            seat.LatestPosition.Should().Be(appendedTo, "the aggregate learns the position it appended to");

            var stored = await StoredSeat(database.Connect());
            stored.ReservedBy.Should().Be("s8");
            stored.Version.Should().Be(2);
            stored.LatestPosition.Should().Be(appendedTo);
        });

    [RequiresDockerFact]
    public Task A_snapshot_only_save_is_one_database_command_and_is_not_checked() =>
        WithDatabase(async database =>
        {
            var first = new SeatAggregate();
            first.Reserve("a1", "s7");
            await database.Connect().SaveAggregate(SeatA1Aggregate, first,
                AppendCondition.NothingAppendedFor(SeatA1Aggregate.Boundary));

            // Nothing to append, so nothing for the condition to guard: as on the Entity Framework
            // Core path, a stale condition does not refuse it.
            var seat = await StoredSeat(database.Connect());
            var commands = new CommandCounter();
            var result = await database.Connect(commands).SaveAggregate(SeatA1Aggregate, seat,
                AppendCondition.NothingAppendedFor(SeatA1Aggregate.Boundary));

            result.IsSuccess.Should().BeTrue(result.Failure?.Description);
            commands.Count.Should().Be(1, $"the save should be one round trip, but sent:\n{commands}");
        });

    [RequiresDockerFact]
    public Task A_refused_aggregate_save_writes_neither_events_nor_snapshot() =>
        WithDatabase(async database =>
        {
            await database.Connect().SaveEvents([Reserved("a1", "s7")], condition: null);
            var latest = await database.Connect().GetLatestPosition(SeatA1Aggregate.Boundary);

            var seat = new SeatAggregate();
            seat.Reserve("a1", "s8");
            var result = await database.Connect().SaveAggregate(SeatA1Aggregate, seat,
                AppendCondition.NothingAppendedFor(SeatA1Aggregate.Boundary));

            result.Failure!.Type.Should().Be(EventSourcing.StoreFailures.ConcurrencyConflictType);
            result.Failure.Tags!["latestPosition"].Should().Be(latest.ToString());
            var reading = database.Connect();
            (await reading.DcbEvents.CountAsync()).Should().Be(1);
            (await reading.DcbSnapshots.CountAsync()).Should().Be(0);
        });

    [RequiresDockerFact]
    public Task A_snapshot_keeps_its_creation_stamp_and_takes_a_new_update_stamp_when_replaced() =>
        WithDatabase(async database =>
        {
            var created = new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
            var time = new FakeTimeProvider(created);
            var user = new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "auditor")], "Test"))
                }
            };

            var first = new SeatAggregate();
            first.Reserve("a1", "s7");
            await database.Connect(time, user).SaveAggregate(SeatA1Aggregate, first,
                AppendCondition.NothingAppendedFor(SeatA1Aggregate.Boundary));

            time.Advance(TimeSpan.FromHours(1));
            var seat = await StoredSeat(database.Connect());
            seat.Reserve("a1", "s8");
            await database.Connect(time, user).SaveAggregate(SeatA1Aggregate, seat,
                new AppendCondition(SeatA1Aggregate.Boundary, seat.LatestPosition));

            var snapshot = await database.Connect().DcbSnapshots.AsNoTracking().SingleAsync();
            snapshot.CreatedDate.Should().Be(created);
            snapshot.CreatedBy.Should().Be("auditor");
            snapshot.UpdatedDate.Should().Be(created.AddHours(1));
            snapshot.UpdatedBy.Should().Be("auditor");
        });

    [RequiresDockerFact]
    public Task Saving_an_older_aggregate_does_not_replace_a_newer_snapshot() =>
        WithDatabase(async database =>
        {
            var first = new SeatAggregate();
            first.Reserve("a1", "s7");
            await database.Connect().SaveAggregate(SeatA1Aggregate, first,
                AppendCondition.NothingAppendedFor(SeatA1Aggregate.Boundary));
            var older = await StoredSeat(database.Connect());

            var newer = await StoredSeat(database.Connect());
            newer.Reserve("a1", "s8");
            await database.Connect().SaveAggregate(SeatA1Aggregate, newer,
                new AppendCondition(SeatA1Aggregate.Boundary, newer.LatestPosition));

            // A snapshot-only save of the instance read before the second save.
            var result = await database.Connect().SaveAggregate(SeatA1Aggregate, older,
                new AppendCondition(SeatA1Aggregate.Boundary, older.LatestPosition));

            result.IsSuccess.Should().BeTrue(result.Failure?.Description);
            var stored = await StoredSeat(database.Connect());
            stored.ReservedBy.Should().Be("s8", "the newer snapshot must survive");
            stored.LatestPosition.Should().Be(newer.LatestPosition);
        });

    [RequiresDockerFact]
    public Task Concurrent_first_snapshots_of_disjoint_aggregates_all_succeed() =>
        WithDatabase(async database =>
        {
            // Every writer inserts a snapshot row nobody has yet: the case a range lock around a
            // missing key would turn into deadlocks between aggregates that share nothing.
            const int writers = 16;
            const int rounds = 5;

            var failures = new List<string>();
            for (var round = 0; round < rounds; round++)
            {
                var thisRound = round;
                failures.AddRange(await AllAtOnce(writers, async (writer, dbContext) =>
                {
                    var seatId = new SeatId($"f{thisRound}-{writer:00}");
                    var seat = new SeatAggregate();
                    seat.Reserve($"f{thisRound}-{writer:00}", "s1");

                    var result = await dbContext.SaveAggregate(seatId, seat, AppendCondition.NothingAppendedFor(seatId.Boundary));

                    return result.IsSuccess ? [] : [$"{seatId.Boundary}: {result.Failure!.Type} {result.Failure.Description}"];
                }, database));
            }

            failures.Should().BeEmpty("aggregates that share nothing never fail each other");
            (await database.Connect().DcbSnapshots.CountAsync()).Should().Be(writers * rounds);
        });

    private static readonly Tag StudentS7 = new("student", "s7");

    [RequiresDockerFact]
    public Task An_append_conditioned_on_an_intersection_is_one_database_command() =>
        WithDatabase(async database =>
        {
            var boundary = TagQuery.AllOf(SeatA1, StudentS7);
            var commands = new CommandCounter();
            var dbContext = database.Connect(commands);

            var first = await dbContext.SaveEvents([Reserved("a1", "s7", SeatA1, StudentS7)],
                AppendCondition.NothingAppendedFor(boundary));
            var stale = await dbContext.SaveEvents([Reserved("a1", "s7", SeatA1, StudentS7)],
                AppendCondition.NothingAppendedFor(boundary));

            first.IsSuccess.Should().BeTrue(first.Failure?.Description);
            stale.Failure!.Type.Should().Be(EventSourcing.StoreFailures.ConcurrencyConflictType);
            commands.Count.Should().Be(2, $"each append should be one round trip, but sent:\n{commands}");
        });

    [RequiresDockerFact]
    public Task An_aggregate_save_conditioned_on_an_intersection_is_one_database_command() =>
        WithDatabase(async database =>
        {
            var seatForStudent = new SeatForStudentId("a1");
            var seat = new SeatAggregate();
            seat.Reserve("a1", "s7");

            var commands = new CommandCounter();
            var result = await database.Connect(commands).SaveAggregate(seatForStudent, seat,
                AppendCondition.NothingAppendedFor(seatForStudent.Boundary));

            result.IsSuccess.Should().BeTrue(result.Failure?.Description);
            commands.Count.Should().Be(1, $"the save should be one round trip, but sent:\n{commands}");
        });

    [RequiresDockerFact]
    public Task An_event_carrying_only_some_of_an_intersection_s_tags_does_not_move_it() =>
        WithDatabase(async database =>
        {
            var dbContext = database.Connect();
            await dbContext.SaveEvents([Reserved("a1", "s8", SeatA1)], condition: null);

            var result = await dbContext.SaveEvents([Reserved("a1", "s7", SeatA1, StudentS7)],
                AppendCondition.NothingAppendedFor(TagQuery.AllOf(SeatA1, StudentS7)));

            result.IsSuccess.Should().BeTrue("an event tagged with the seat alone is outside the seat-and-student boundary");
        });

    [RequiresDockerFact]
    public Task An_append_on_an_intersection_waiting_on_a_tag_head_is_refused_when_the_holder_appends_into_it() =>
        WithDatabase(async database =>
        {
            var result = await AppendWhileTheHolderCommits(database.ConnectionString,
                [Reserved("a1", "s7", SeatA1, StudentS7)],
                AppendCondition.NothingAppendedFor(TagQuery.AllOf(SeatA1, StudentS7)),
                holderTags: [SeatA1, StudentS7]);

            result.Failure!.Type.Should().Be(EventSourcing.StoreFailures.ConcurrencyConflictType,
                "the holder appended an event carrying every tag of the boundary");
        });

    [RequiresDockerFact]
    public Task An_append_on_an_intersection_waiting_on_a_tag_head_is_accepted_when_the_holder_appends_beside_it() =>
        WithDatabase(async database =>
        {
            var result = await AppendWhileTheHolderCommits(database.ConnectionString,
                [Reserved("a1", "s7", SeatA1, StudentS7)],
                AppendCondition.NothingAppendedFor(TagQuery.AllOf(SeatA1, StudentS7)),
                holderTags: [SeatA1]);

            result.IsSuccess.Should().BeTrue("the holder's event carries the seat but not the student");
        });

    /// <summary>
    /// Holds the first tag head the append contends on, starts the append so it waits for it, then
    /// commits an event under <paramref name="holderTags"/> and releases the head, returning what the
    /// append decided.
    /// </summary>
    private static async Task<Memoria.Results.Result> AppendWhileTheHolderCommits(string connectionString,
        TaggedEvent[] events, AppendCondition condition, Tag[] holderTags, TestDbContext? appending = null)
    {
        await using var holder = DcbStoreSchema.OnSqlServer(connectionString);
        await using var appendingByDefault = DcbStoreSchema.OnSqlServer(connectionString);

        // The heads have to exist for the holder to lock one; an unrelated append creates them.
        await holder.SaveEvents([Reserved("z9", "z9", holderTags.Concat(condition.Query.Tags).Distinct().ToArray())],
            condition: null);
        var position = await holder.GetLatestPosition(condition.Query);
        condition = new AppendCondition(condition.Query, position);

        await using var holding = await holder.Database.BeginTransactionAsync();
        var held = condition.Query.Tags.Select(tag => tag.ToString()).Order(StringComparer.Ordinal).First();
        await holder.Database.ExecuteSqlRawAsync("UPDATE [DcbTagHeads] SET [Token] = NEWID() WHERE [Tag] = {0}", held);

        var waiting = (appending ?? appendingByDefault).SaveEvents(events, condition);
        await UntilAnAppendIsWaitingOnALock(connectionString);

        await holder.Database.ExecuteSqlRawAsync(
            """
            INSERT [DcbEvents] ([EventType], [Data], [CreatedDate]) VALUES ('SeatReserved:1', {0}, SYSDATETIMEOFFSET());
            DECLARE @position bigint = CAST(SCOPE_IDENTITY() AS bigint);
            INSERT [DcbEventTags] ([Position], [Tag]) SELECT @position, [value] FROM OPENJSON({1});
            """,
            DomainSerializer.Current.Serialize(new SeatReservedEvent("a1", "s8")),
            System.Text.Json.JsonSerializer.Serialize(holderTags.Select(tag => tag.ToString())));
        await holding.CommitAsync();

        return await waiting;
    }

    [RequiresDockerFact]
    public Task An_unconditional_append_is_one_database_command() =>
        WithDatabase(async database =>
        {
            var commands = new CommandCounter();

            var result = await database.Connect(commands).SaveEvents([Reserved("a1", "s7")], condition: null);

            result.IsSuccess.Should().BeTrue(result.Failure?.Description);
            commands.Count.Should().Be(1, $"the append should be one round trip, but sent:\n{commands}");
        });

    [RequiresDockerFact]
    public Task An_unconditional_aggregate_save_is_one_database_command() =>
        WithDatabase(async database =>
        {
            var seat = new SeatAggregate();
            seat.Reserve("a1", "s7");

            var commands = new CommandCounter();
            var result = await database.Connect(commands).SaveAggregate(SeatA1Aggregate, seat, condition: null);

            result.IsSuccess.Should().BeTrue(result.Failure?.Description);
            commands.Count.Should().Be(1, $"the save should be one round trip, but sent:\n{commands}");
            (await StoredSeat(database.Connect())).ReservedBy.Should().Be("s7");
        });

    [RequiresDockerFact]
    public Task An_unconditional_append_refuses_a_conditioned_append_that_read_before_it() =>
        WithDatabase(async database =>
        {
            var dbContext = database.Connect();
            await dbContext.SaveEvents([Reserved("a1", "s7")], condition: null);
            var position = await dbContext.GetLatestPosition(TagQuery.AnyOf(SeatA1));

            var unconditional = await dbContext.SaveEvents([Reserved("a1", "s8")], condition: null);
            var conditioned = await dbContext.SaveEvents([Reserved("a1", "s9")],
                new AppendCondition(TagQuery.AnyOf(SeatA1), position));

            unconditional.IsSuccess.Should().BeTrue(unconditional.Failure?.Description);
            conditioned.Failure!.Type.Should().Be(EventSourcing.StoreFailures.ConcurrencyConflictType);
        });

    [RequiresDockerFact]
    public Task Concurrent_unconditional_first_saves_of_disjoint_aggregates_all_succeed() =>
        WithDatabase(async database =>
        {
            // S2 found these refusing each other on the Entity Framework Core path.
            const int writers = 16;
            const int rounds = 5;

            var failures = new List<string>();
            for (var round = 0; round < rounds; round++)
            {
                var thisRound = round;
                failures.AddRange(await AllAtOnce(writers, async (writer, dbContext) =>
                {
                    var seatId = new SeatId($"u{thisRound}-{writer:00}");
                    var seat = new SeatAggregate();
                    seat.Reserve($"u{thisRound}-{writer:00}", "s1");

                    var result = await dbContext.SaveAggregate(seatId, seat, condition: null);

                    return result.IsSuccess ? [] : [$"{seatId.Boundary}: {result.Failure!.Type} {result.Failure.Description}"];
                }, database));
            }

            failures.Should().BeEmpty("unconditional saves of aggregates that share nothing never fail each other");
            (await database.Connect().DcbSnapshots.CountAsync()).Should().Be(writers * rounds);
        });

    [RequiresDockerFact]
    public Task A_change_staged_on_the_context_commits_with_the_append() =>
        WithStagingDatabase(async database =>
        {
            await using var dbContext = database.ConnectStaging();
            dbContext.Bookings.Add(new PostgreSqlSingleCallAppendTests.Booking { Id = "b1", Seat = "a1" });

            var result = await dbContext.SaveEvents([Reserved("a1", "s7")],
                AppendCondition.NothingAppendedFor(TagQuery.AnyOf(SeatA1)));

            result.IsSuccess.Should().BeTrue(result.Failure?.Description);
            dbContext.ChangeTracker.HasChanges().Should().BeFalse(
                "what was staged is now saved; leaving it pending would insert it again on the next save");
            await using var reading = database.ConnectStaging();
            (await reading.Bookings.CountAsync()).Should().Be(1);
            (await reading.DcbEvents.CountAsync()).Should().Be(1);
        });

    [RequiresDockerFact]
    public Task A_change_staged_on_the_context_commits_with_an_aggregate_save() =>
        WithStagingDatabase(async database =>
        {
            await using var dbContext = database.ConnectStaging();
            dbContext.Bookings.Add(new PostgreSqlSingleCallAppendTests.Booking { Id = "b1", Seat = "a1" });
            var seat = new SeatAggregate();
            seat.Reserve("a1", "s7");

            var result = await dbContext.SaveAggregate(SeatA1Aggregate, seat,
                AppendCondition.NothingAppendedFor(SeatA1Aggregate.Boundary));

            result.IsSuccess.Should().BeTrue(result.Failure?.Description);
            await using var reading = database.ConnectStaging();
            (await reading.Bookings.CountAsync()).Should().Be(1);
            (await reading.DcbSnapshots.CountAsync()).Should().Be(1);
        });

    [RequiresDockerFact]
    public Task A_refused_append_saves_no_staged_change_and_leaves_it_pending() =>
        WithStagingDatabase(async database =>
        {
            await using var dbContext = database.ConnectStaging();
            await dbContext.SaveEvents([Reserved("a1", "s7")], condition: null);
            dbContext.Bookings.Add(new PostgreSqlSingleCallAppendTests.Booking { Id = "b1", Seat = "a1" });

            var result = await dbContext.SaveEvents([Reserved("a1", "s8", SeatA1, StudentS7)],
                AppendCondition.NothingAppendedFor(TagQuery.AnyOf(SeatA1)));

            result.Failure!.Type.Should().Be(EventSourcing.StoreFailures.ConcurrencyConflictType);
            dbContext.ChangeTracker.HasChanges().Should().BeTrue("the caller still holds what it staged");
            await using var reading = database.ConnectStaging();
            (await reading.Bookings.CountAsync()).Should().Be(0);
            (await reading.DcbEvents.CountAsync()).Should().Be(1);
        });

    [RequiresDockerFact]
    public Task A_refusal_decided_with_the_locks_held_saves_no_staged_change_and_leaves_the_context_usable() =>
        WithStagingDatabase(async database =>
        {
            // The procedure refuses here after it has taken the locks, inside the transaction the
            // staged change was saved in: it must undo only its own work, not end that transaction.
            await using var dbContext = database.ConnectStaging();
            dbContext.Bookings.Add(new PostgreSqlSingleCallAppendTests.Booking { Id = "b1", Seat = "a1" });

            var refused = await AppendWhileTheHolderCommits(database.ConnectionString, [Reserved("a1", "s7")],
                AppendCondition.NothingAppendedFor(TagQuery.AnyOf(SeatA1)), holderTags: [SeatA1], dbContext);

            refused.Failure!.Type.Should().Be(EventSourcing.StoreFailures.ConcurrencyConflictType, refused.Failure.Description);
            dbContext.ChangeTracker.HasChanges().Should().BeTrue("the caller still holds what it staged");
            await using (var reading = database.ConnectStaging())
            {
                (await reading.Bookings.CountAsync()).Should().Be(0);
            }

            var latest = await dbContext.GetLatestPosition(TagQuery.AnyOf(SeatA1));
            var accepted = await dbContext.SaveEvents([Reserved("a1", "s9")],
                new AppendCondition(TagQuery.AnyOf(SeatA1), latest));

            accepted.IsSuccess.Should().BeTrue(accepted.Failure?.Description);
            await using var after = database.ConnectStaging();
            (await after.Bookings.CountAsync()).Should().Be(1, "the next accepted append saves what was staged");
        });

    [RequiresDockerFact]
    public Task A_refusal_decided_with_the_locks_held_leaves_the_caller_s_own_transaction_usable() =>
        WithDatabase(async database =>
        {
            // The procedure joins a transaction the caller opened, and a refusal must undo only its
            // own work: the caller can go on to append again and commit what it chose to.
            await using var dbContext = DcbStoreSchema.OnSqlServer(database.ConnectionString);
            await using var transaction = await dbContext.Database.BeginTransactionAsync();

            var refused = await AppendWhileTheHolderCommits(database.ConnectionString, [Reserved("a1", "s7")],
                AppendCondition.NothingAppendedFor(TagQuery.AnyOf(SeatA1)), holderTags: [SeatA1], dbContext);

            refused.Failure!.Type.Should().Be(EventSourcing.StoreFailures.ConcurrencyConflictType, refused.Failure.Description);

            var latest = await dbContext.GetLatestPosition(TagQuery.AnyOf(SeatA1));
            var accepted = await dbContext.SaveEvents([Reserved("a1", "s9")],
                new AppendCondition(TagQuery.AnyOf(SeatA1), latest));
            accepted.IsSuccess.Should().BeTrue(accepted.Failure?.Description);
            await transaction.CommitAsync();

            (await database.Connect().DcbEvents.CountAsync()).Should().Be(3,
                "the seeding append, the holder's and the caller's accepted append");
        });

    [RequiresDockerFact]
    public Task A_staged_change_that_cannot_be_saved_fails_the_append() =>
        WithStagingDatabase(async database =>
        {
            await using (var existing = database.ConnectStaging())
            {
                existing.Bookings.Add(new PostgreSqlSingleCallAppendTests.Booking { Id = "b1", Seat = "a1" });
                await existing.SaveChangesAsync();
            }

            await using var dbContext = database.ConnectStaging();
            dbContext.Bookings.Add(new PostgreSqlSingleCallAppendTests.Booking { Id = "b1", Seat = "a2" });

            var result = await dbContext.SaveEvents([Reserved("a1", "s7", SeatA1, StudentS7)],
                AppendCondition.NothingAppendedFor(TagQuery.AnyOf(SeatA1)));

            result.Failure!.Type.Should().Be(EventSourcing.StoreFailures.StorageFailureType);
            await using var reading = database.ConnectStaging();
            (await reading.DcbEvents.CountAsync()).Should().Be(0, "nothing is appended when the staged change fails");
            (await reading.Bookings.SingleAsync()).Seat.Should().Be("a1");
        });

    [RequiresDockerFact]
    public Task A_retried_append_saves_its_staged_change_again() =>
        WithStagingDatabase(async database =>
        {
            var failOnce = new FailTheProcedureOnce();
            await using var dbContext = database.ConnectStaging(retrying: true, interceptors: failOnce);
            dbContext.Bookings.Add(new PostgreSqlSingleCallAppendTests.Booking { Id = "b1", Seat = "a1" });

            var result = await dbContext.SaveEvents([Reserved("a1", "s7")],
                AppendCondition.NothingAppendedFor(TagQuery.AnyOf(SeatA1)));

            failOnce.Failed.Should().BeTrue("the first attempt must actually have failed");
            result.IsSuccess.Should().BeTrue(result.Failure?.Description);
            await using var reading = database.ConnectStaging();
            (await reading.Bookings.CountAsync()).Should().Be(1, "the retry saves the staged change it rolled back");
            (await reading.DcbEvents.CountAsync()).Should().Be(1);
        });

    [RequiresDockerFact]
    public Task Concurrent_appends_with_staged_changes_each_introducing_a_new_tag_all_succeed() =>
        WithStagingDatabase(async database =>
        {
            // Inside the caller's transaction the procedure creates missing heads in it, so a
            // duplicate key from a head another append created must not doom that transaction.
            const int writers = 16;
            const int rounds = 5;
            var failures = new ConcurrentBag<string>();

            for (var round = 0; round < rounds; round++)
            {
                using var start = new Barrier(writers);
                var thisRound = round;

                await Task.WhenAll(Enumerable.Range(0, writers).Select(writer => Task.Run(async () =>
                {
                    await using var dbContext = database.ConnectStaging();
                    await dbContext.Database.OpenConnectionAsync();
                    var seat = new Tag("seat", $"n{thisRound}-{writer:00}");
                    dbContext.Bookings.Add(new PostgreSqlSingleCallAppendTests.Booking
                    {
                        Id = $"b{thisRound}-{writer:00}", Seat = seat.ToString()
                    });
                    start.SignalAndWait();

                    // A shared tag too, so writers race to create the same head as well as their own.
                    var result = await dbContext.SaveEvents(
                        [Reserved($"n{thisRound}-{writer:00}", "s1", seat, new Tag("round", $"{thisRound}"))],
                        AppendCondition.NothingAppendedFor(TagQuery.AnyOf(seat)));

                    if (result.IsNotSuccess)
                    {
                        failures.Add($"{seat}: {result.Failure!.Type} {result.Failure.Description}");
                    }
                })));
            }

            failures.Should().BeEmpty("appends introducing different tags never fail each other");
            await using var reading = database.ConnectStaging();
            (await reading.Bookings.CountAsync()).Should().Be(writers * rounds);
        });

    [RequiresDockerFact]
    public Task Procedure_and_entity_framework_core_appends_racing_on_one_boundary_are_never_both_accepted() =>
        WithDatabase(async database =>
        {
            // Half the writers through the procedure, half through a context the store appends for
            // through Entity Framework Core, all on one boundary.
            const int writers = 8;
            const int appendsEach = 25;
            var acceptedOn = new ConcurrentBag<long>();
            var failures = new ConcurrentBag<string>();
            var boundary = TagQuery.AnyOf(SeatA1);

            await Task.WhenAll(Enumerable.Range(0, writers).Select(writer => Task.Run(async () =>
            {
                await using DbContext dbContext = writer % 2 == 0
                    ? DcbStoreSchema.OnSqlServer(database.ConnectionString)
                    : InterfaceOnlyDcbDbContext.OnSqlServer(database.ConnectionString);
                var store = (IDcbDbContext)dbContext;

                for (var accepted = 0; accepted < appendsEach;)
                {
                    var seen = await store.GetLatestPosition(boundary);
                    var result = await store.SaveEvents([Reserved("a1", $"s{writer}-{accepted}")],
                        new AppendCondition(boundary, seen));

                    if (result.IsSuccess)
                    {
                        acceptedOn.Add(seen);
                        accepted++;
                    }
                    else if (result.Failure!.Type != EventSourcing.StoreFailures.ConcurrencyConflictType)
                    {
                        failures.Add($"writer {writer}: {result.Failure.Type} {result.Failure.Description}");
                        accepted++;
                    }
                }
            })));

            failures.Should().BeEmpty("a writer losing a race is refused, never failed");
            acceptedOn.Should().OnlyHaveUniqueItems("no two appends may be accepted against the same position");
        });

    /// <summary>A database with the store's schema plus an entity of the caller's own.</summary>
    private sealed class StagingDatabase(string connectionString)
    {
        public string ConnectionString => connectionString;

        public PostgreSqlSingleCallAppendTests.StagingDbContext ConnectStaging(params IInterceptor[] interceptors) =>
            ConnectStaging(retrying: false, interceptors);

        public PostgreSqlSingleCallAppendTests.StagingDbContext ConnectStaging(bool retrying,
            params IInterceptor[] interceptors) =>
            new(new DbContextOptionsBuilder<DcbDbContext>()
                .UseSqlServer(connectionString, sqlServer =>
                {
                    if (retrying)
                    {
                        sqlServer.EnableRetryOnFailure();
                    }
                })
                .AddInterceptors(interceptors)
                .Options);
    }

    private Task WithStagingDatabase(Func<StagingDatabase, Task> body) =>
        WithDatabase(async database =>
        {
            var staging = new StagingDatabase(database.ConnectionString);

            // The store's schema is already there; the caller's own table is added beside it.
            await using (var setup = staging.ConnectStaging())
            {
                await setup.Database.ExecuteSqlRawAsync(
                    "CREATE TABLE [Bookings] ([Id] nvarchar(450) NOT NULL PRIMARY KEY, [Seat] nvarchar(max) NOT NULL)");
            }

            await body(staging);
        });

    /// <summary>Fails the first call to the append procedure with an error the retrying strategy retries.</summary>
    private sealed class FailTheProcedureOnce : DbCommandInterceptor
    {
        public bool Failed { get; private set; }

        public override ValueTask<InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
            System.Data.Common.DbCommand command, CommandEventData eventData,
            InterceptionResult<System.Data.Common.DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (!Failed && command.CommandText.Contains("memoria_dcb_append"))
            {
                Failed = true;
                throw new TimeoutException("Simulated transient failure.");
            }

            return ValueTask.FromResult(result);
        }
    }
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
