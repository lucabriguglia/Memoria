using System.Collections.Concurrent;
using System.Data.Common;
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
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Xunit;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests;

/// <summary>
/// On PostgreSQL a DCB append is one call to the store's append routine, rather than the reads,
/// writes and commit the Entity Framework Core path sends.
/// </summary>
/// <remarks>
/// What an append decides is covered by <see cref="DcbStoreOnEngineTests"/>, which runs on
/// PostgreSQL through the routine too. These cover what is particular to the routine: how many round
/// trips it costs, the race it has instead of the one the Entity Framework Core path guards against,
/// and the failure and audit behaviour it reaches by a different route.
/// </remarks>
[Trait("Category", "Container")]
[Collection(PostgreSqlCollection.Name)]
public class PostgreSqlSingleCallAppendTests(PostgreSqlFixture fixture)
{
    private static readonly Tag SeatA1 = new("seat", "a1");
    private static readonly Tag StudentS7 = new("student", "s7");

    private static TaggedEvent Reserved(string seat, string student, params Tag[] tags) =>
        new(new SeatReservedEvent(seat, student), tags.Length > 0 ? tags : [new Tag("seat", seat)]);

    /// <summary>A fresh database with the store's schema, and contexts onto it.</summary>
    private sealed class TestDatabase(string connectionString, List<TestDbContext> opened)
    {
        public string ConnectionString => connectionString;

        public TestDbContext Connect(params IInterceptor[] interceptors) =>
            Track(DcbStoreSchema.OnPostgreSql(connectionString, interceptors));

        public TestDbContext Connect(TimeProvider timeProvider, IHttpContextAccessor httpContextAccessor) =>
            Track(new TestDbContext(new DbContextOptionsBuilder<DcbDbContext>().UseNpgsql(connectionString).Options,
                timeProvider, httpContextAccessor));

        private TestDbContext Track(TestDbContext context)
        {
            opened.Add(context);
            return context;
        }
    }

    /// <summary>
    /// Runs the body against a fresh database with the store's schema and, unless told otherwise, its
    /// append routine.
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

            NpgsqlConnection.ClearAllPools();

            try
            {
                await using var cleanup = DcbStoreSchema.OnPostgreSql(database.ConnectionString);
                await cleanup.Database.EnsureDeletedAsync();
            }
            catch
            {
                // The fixture drops what is left; cleanup must not mask the result.
            }
        }
    }

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
            // The race the routine has instead of the one the token guard covers: it locks before it
            // checks, so an append that passed the early check can wait on a head while the holder
            // commits into its boundary. The check it makes with the locks held has to see that.
            var seeding = database.Connect();
            await seeding.SaveEvents([Reserved("a1", "s7")], condition: null);
            var position = await seeding.GetLatestPosition(TagQuery.AnyOf(SeatA1));

            var holder = database.Connect();
            await using var holding = await holder.Database.BeginTransactionAsync();
            await holder.Database.ExecuteSqlRawAsync(
                "SELECT 1 FROM \"DcbTagHeads\" WHERE \"Tag\" = {0} FOR UPDATE", SeatA1.ToString());

            var waiting = database.Connect().SaveEvents([Reserved("a1", "s9")],
                new AppendCondition(TagQuery.AnyOf(SeatA1), position));

            await UntilAnAppendIsWaitingOnALock(database.ConnectionString);

            await holder.Database.ExecuteSqlRawAsync(
                """
                WITH written AS (
                    INSERT INTO "DcbEvents" ("EventType", "Data", "CreatedDate")
                    VALUES ('SeatReserved:1', {1}, now())
                    RETURNING "Position")
                INSERT INTO "DcbEventTags" ("Position", "Tag") SELECT "Position", {0} FROM written
                """, SeatA1.ToString(), DomainSerializer.Current.Serialize(new SeatReservedEvent("a1", "s8")));
            await holding.CommitAsync();

            var result = await waiting;

            result.IsNotSuccess.Should().BeTrue("the holder appended into the boundary while the append waited");
            result.Failure!.Type.Should().Be(EventSourcing.StoreFailures.ConcurrencyConflictType);
            result.Failure.Tags!["latestPosition"].Should().Be((position + 1).ToString());
        });

    [RequiresDockerFact]
    public Task Concurrent_appends_on_one_boundary_are_never_both_accepted_on_the_same_position() =>
        WithDatabase(async database =>
        {
            const int writers = 8;
            const int appendsEach = 10;
            var acceptedOn = new ConcurrentBag<long>();
            var boundary = TagQuery.AnyOf(SeatA1);

            await Task.WhenAll(Enumerable.Range(0, writers).Select(writer => Task.Run(async () =>
            {
                await using var dbContext = DcbStoreSchema.OnPostgreSql(database.ConnectionString);

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
    public Task An_append_through_the_routine_is_stamped_as_the_entity_framework_core_path_stamps_it() =>
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

            // Conditioned goes through the routine; unconditional still goes through Entity Framework
            // Core, so the second is the reference the first must match.
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
    public Task An_append_without_the_routine_installed_says_how_to_install_it() =>
        WithDatabase(async database =>
        {
            var result = await database.Connect().SaveEvents([Reserved("a1", "s7")],
                AppendCondition.NothingAppendedFor(TagQuery.AnyOf(SeatA1)));

            result.IsNotSuccess.Should().BeTrue();
            result.Failure!.Type.Should().Be(EventSourcing.StoreFailures.StorageFailureType);
            result.Failure.Description.Should().Contain("EnsureDcbAppendRoutineAsync")
                .And.Contain("migration")
                .And.Contain("install script");
        }, installRoutine: false);

    [RequiresDockerFact]
    public Task Installing_the_routine_twice_is_harmless() =>
        WithDatabase(async database =>
        {
            var dbContext = database.Connect();

            await dbContext.Database.EnsureDcbAppendRoutineAsync();

            var result = await dbContext.SaveEvents([Reserved("a1", "s7")],
                AppendCondition.NothingAppendedFor(TagQuery.AnyOf(SeatA1)));
            result.IsSuccess.Should().BeTrue(result.Failure?.Description);
        });

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
            var boundary = TagQuery.AllOf(SeatA1, StudentS7);

            var result = await AppendWhileTheHolderCommits(database,
                [Reserved("a1", "s7", SeatA1, StudentS7)], AppendCondition.NothingAppendedFor(boundary),
                holderTags: [SeatA1, StudentS7]);

            result.Failure!.Type.Should().Be(EventSourcing.StoreFailures.ConcurrencyConflictType,
                "the holder appended an event carrying every tag of the boundary");
        });

    [RequiresDockerFact]
    public Task An_append_on_an_intersection_waiting_on_a_tag_head_is_accepted_when_the_holder_appends_beside_it() =>
        WithDatabase(async database =>
        {
            var boundary = TagQuery.AllOf(SeatA1, StudentS7);

            var result = await AppendWhileTheHolderCommits(database,
                [Reserved("a1", "s7", SeatA1, StudentS7)], AppendCondition.NothingAppendedFor(boundary),
                holderTags: [SeatA1]);

            result.IsSuccess.Should().BeTrue("the holder's event carries the seat but not the student");
        });

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
    public Task A_change_staged_on_the_context_commits_with_the_append() =>
        WithStagingDatabase(async database =>
        {
            var commands = new CommandCounter();
            await using var dbContext = database.ConnectStaging(commands);
            dbContext.Bookings.Add(new Booking { Id = "b1", Seat = "a1" });

            var result = await dbContext.SaveEvents([Reserved("a1", "s7")],
                AppendCondition.NothingAppendedFor(TagQuery.AnyOf(SeatA1)));

            result.IsSuccess.Should().BeTrue(result.Failure?.Description);
            commands.Count.Should().Be(2, $"the staged change and the append, but sent:\n{commands}");
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
            dbContext.Bookings.Add(new Booking { Id = "b1", Seat = "a1" });
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
            dbContext.Bookings.Add(new Booking { Id = "b1", Seat = "a1" });

            // A new tag as well, so the append would have to create a head: on the Entity Framework
            // Core path that is where staged changes escaped the transaction.
            var result = await dbContext.SaveEvents([Reserved("a1", "s8", SeatA1, StudentS7)],
                AppendCondition.NothingAppendedFor(TagQuery.AnyOf(SeatA1)));

            result.Failure!.Type.Should().Be(EventSourcing.StoreFailures.ConcurrencyConflictType);
            dbContext.ChangeTracker.HasChanges().Should().BeTrue("the caller still holds what it staged");
            await using var reading = database.ConnectStaging();
            (await reading.Bookings.CountAsync()).Should().Be(0);
            (await reading.DcbEvents.CountAsync()).Should().Be(1);
        });

    [RequiresDockerFact]
    public Task A_staged_change_that_cannot_be_saved_fails_the_append() =>
        WithStagingDatabase(async database =>
        {
            await using (var existing = database.ConnectStaging())
            {
                existing.Bookings.Add(new Booking { Id = "b1", Seat = "a1" });
                await existing.SaveChangesAsync();
            }

            await using var dbContext = database.ConnectStaging();
            dbContext.Bookings.Add(new Booking { Id = "b1", Seat = "a2" });

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
            var failOnce = new FailTheRoutineOnce();
            await using var dbContext = database.ConnectStaging(retrying: true, interceptors: failOnce);
            dbContext.Bookings.Add(new Booking { Id = "b1", Seat = "a1" });

            var result = await dbContext.SaveEvents([Reserved("a1", "s7")],
                AppendCondition.NothingAppendedFor(TagQuery.AnyOf(SeatA1)));

            failOnce.Failed.Should().BeTrue("the first attempt must actually have failed");
            result.IsSuccess.Should().BeTrue(result.Failure?.Description);
            await using var reading = database.ConnectStaging();
            (await reading.Bookings.CountAsync()).Should().Be(1, "the retry saves the staged change it rolled back");
            (await reading.DcbEvents.CountAsync()).Should().Be(1);
        });

    /// <summary>A database with the store's schema plus an entity of the caller's own.</summary>
    private sealed class StagingDatabase(string connectionString)
    {
        public StagingDbContext ConnectStaging(params IInterceptor[] interceptors) =>
            ConnectStaging(retrying: false, interceptors);

        public StagingDbContext ConnectStaging(bool retrying, params IInterceptor[] interceptors) =>
            new(new DbContextOptionsBuilder<DcbDbContext>()
                .UseNpgsql(connectionString, npgsql =>
                {
                    if (retrying)
                    {
                        npgsql.EnableRetryOnFailure();
                    }
                })
                .AddInterceptors(interceptors)
                .Options);
    }

    private Task WithStagingDatabase(Func<StagingDatabase, Task> body) =>
        WithDatabase(async database =>
        {
            var staging = new StagingDatabase(database.ConnectionString);

            await using (var setup = staging.ConnectStaging())
            {
                await setup.Database.EnsureDeletedAsync();
                await setup.Database.EnsureCreatedAsync();
                await setup.Database.EnsureDcbAppendRoutineAsync();
            }

            await body(staging);
        });

    public sealed class Booking
    {
        public string Id { get; set; } = null!;

        public string Seat { get; set; } = null!;
    }

    public sealed class StagingDbContext(DbContextOptions<DcbDbContext> options)
        : TestDbContext(options, TimeProvider.System, new StubHttpContextAccessor())
    {
        public DbSet<Booking> Bookings => Set<Booking>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Booking>().HasKey(booking => booking.Id);
        }
    }

    /// <summary>Fails the first call to the append routine with a transient error.</summary>
    private sealed class FailTheRoutineOnce : DbCommandInterceptor
    {
        public bool Failed { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (!Failed && command.CommandText.Contains("memoria_dcb_append"))
            {
                Failed = true;
                throw new NpgsqlException("Simulated transient failure.", new TimeoutException());
            }

            return ValueTask.FromResult(result);
        }
    }

    /// <summary>
    /// Holds the first tag head the append contends on, starts the append so it waits for it, then
    /// commits an event under <paramref name="holderTags"/> and releases the head, returning what the
    /// append decided.
    /// </summary>
    private static async Task<Memoria.Results.Result> AppendWhileTheHolderCommits(TestDatabase database,
        TaggedEvent[] events, AppendCondition condition, Tag[] holderTags)
    {
        var holder = database.Connect();
        await holder.Database.EnsureDcbAppendRoutineAsync();

        // The heads have to exist for the holder to lock one; an unrelated append creates them.
        await holder.SaveEvents([Reserved("z9", "z9", holderTags.Concat(condition.Query.Tags).Distinct().ToArray())],
            condition: null);
        var position = await holder.GetLatestPosition(condition.Query);
        condition = new AppendCondition(condition.Query, position);

        await using var holding = await holder.Database.BeginTransactionAsync();
        var held = condition.Query.Tags.Select(tag => tag.ToString()).Order(StringComparer.Ordinal).First();
        await holder.Database.ExecuteSqlRawAsync(
            "SELECT 1 FROM \"DcbTagHeads\" WHERE \"Tag\" = {0} FOR UPDATE", held);

        var waiting = database.Connect().SaveEvents(events, condition);
        await UntilAnAppendIsWaitingOnALock(database.ConnectionString);

        await holder.Database.ExecuteSqlRawAsync(
            """
            WITH written AS (
                INSERT INTO "DcbEvents" ("EventType", "Data", "CreatedDate")
                VALUES ('SeatReserved:1', {0}, now())
                RETURNING "Position")
            INSERT INTO "DcbEventTags" ("Position", "Tag")
            SELECT written."Position", tag FROM written CROSS JOIN unnest({1}::text[]) AS tag
            """,
            DomainSerializer.Current.Serialize(new SeatReservedEvent("a1", "s8")),
            holderTags.Select(tag => tag.ToString()).ToArray());
        await holding.CommitAsync();

        return await waiting;
    }

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

            var seat = new SeatAggregate();
            seat.Reserve("a1", "s8");
            var result = await database.Connect().SaveAggregate(SeatA1Aggregate, seat,
                AppendCondition.NothingAppendedFor(SeatA1Aggregate.Boundary));

            result.Failure!.Type.Should().Be(EventSourcing.StoreFailures.ConcurrencyConflictType);
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
    public Task A_model_with_its_events_mapped_elsewhere_keeps_appending_through_entity_framework_core() =>
        WithDatabase(async database =>
        {
            // The routine is written for the default tables; until it is generated from the model,
            // a context that maps them elsewhere must not call it.
            await using var remapped = new RemappedEventsDbContext(
                new DbContextOptionsBuilder<DcbDbContext>().UseNpgsql(database.ConnectionString).Options);
            await remapped.Database.EnsureDeletedAsync();
            await remapped.Database.EnsureCreatedAsync();
            await remapped.Database.EnsureDcbAppendRoutineAsync();

            var result = await remapped.SaveEvents([Reserved("a1", "s7")],
                AppendCondition.NothingAppendedFor(TagQuery.AnyOf(SeatA1)));

            result.IsSuccess.Should().BeTrue(result.Failure?.Description);
            (await remapped.DcbEvents.CountAsync()).Should().Be(1);
        });

    private sealed class RemappedEventsDbContext(DbContextOptions<DcbDbContext> options)
        : TestDbContext(options, TimeProvider.System, new StubHttpContextAccessor())
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities.DcbEventEntity>()
                .ToTable("BoxOfficeEvents");
        }
    }

    /// <summary>Waits until an append routine call on this database is blocked on a lock.</summary>
    private static async Task UntilAnAppendIsWaitingOnALock(string connectionString)
    {
        var database = new NpgsqlConnectionStringBuilder(connectionString).Database;
        await using var watcher = new NpgsqlConnection(connectionString);
        await watcher.OpenAsync();

        for (var attempt = 0; attempt < 200; attempt++)
        {
            await using var waiting = new NpgsqlCommand(
                """
                SELECT count(*) FROM pg_stat_activity
                WHERE datname = @database AND wait_event_type = 'Lock' AND query LIKE '%memoria_dcb_append%'
                """, watcher);
            waiting.Parameters.AddWithValue("database", database!);

            if ((long)(await waiting.ExecuteScalarAsync())! > 0)
            {
                return;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException("The append never waited on the held tag head, so the race was not simulated.");
    }
}

/// <summary>
/// <see cref="DcbAppendRoutineDatabaseExtensions.EnsureDcbAppendRoutineAsync"/> on an engine whose
/// appends go through Entity Framework Core: nothing to install, nothing to fail.
/// </summary>
public class AppendRoutineOnOtherEnginesTests
{
    [Fact]
    public async Task Ensuring_the_routine_on_sqlite_does_nothing_and_appends_still_work()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var dbContext = new TestDbContext(
            new DbContextOptionsBuilder<DcbDbContext>().UseSqlite(connection).Options,
            TimeProvider.System, new StubHttpContextAccessor());
        TypeBindings.EventTypeBindings = new Dictionary<string, Type> { { "SeatReserved:1", typeof(SeatReservedEvent) } };
        await dbContext.Database.EnsureCreatedAsync();

        await dbContext.Database.EnsureDcbAppendRoutineAsync();

        var result = await dbContext.SaveEvents(
            [new TaggedEvent(new SeatReservedEvent("a1", "s7"), [new Tag("seat", "a1")])],
            AppendCondition.NothingAppendedFor(TagQuery.AnyOf(new Tag("seat", "a1"))));
        result.IsSuccess.Should().BeTrue(result.Failure?.Description);
    }
}

/// <summary>Records every command a context sends to the database.</summary>
internal sealed class CommandCounter : DbCommandInterceptor
{
    private readonly List<string> _sent = [];

    public int Count
    {
        get
        {
            lock (_sent)
            {
                return _sent.Count;
            }
        }
    }

    public override string ToString()
    {
        lock (_sent)
        {
            return string.Join("\n", _sent);
        }
    }

    private void Record(DbCommand command)
    {
        lock (_sent)
        {
            _sent.Add(command.CommandText.Length <= 120 ? command.CommandText : command.CommandText[..120] + "…");
        }
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        Record(command);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Record(command);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData,
        InterceptionResult<int> result)
    {
        Record(command);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Record(command);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData,
        InterceptionResult<object> result)
    {
        Record(command);
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
    {
        Record(command);
        return ValueTask.FromResult(result);
    }
}
