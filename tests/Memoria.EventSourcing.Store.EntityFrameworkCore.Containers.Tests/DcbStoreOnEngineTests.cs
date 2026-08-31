using FluentAssertions;
using Memoria.EventSourcing.Dcb;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Relational.Tests;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Relational.Tests.Models;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests;

/// <summary>
/// The append condition, against the engines the store actually targets.
/// </summary>
/// <remarks>
/// <para>
/// The SQLite suite proves the algorithm; it cannot prove the mapping. The tag head token is an
/// application-assigned <c>uniqueidentifier</c>/<c>uuid</c> rather than a provider-native
/// <c>rowversion</c> or <c>xmin</c>, the conditioned and unconditional paths emit different SQL
/// (a tracked update with a <c>WHERE Token = @old</c> guard, versus <c>ExecuteUpdate</c>), and both
/// engines resolve that differently. Removing the guard from the model was verified to fail the
/// SQLite suite; these confirm the same holds where it ships.
/// </para>
/// <para>
/// Each test stands up its own database, so nothing leaks between them.
/// </para>
/// </remarks>
public abstract partial class DcbStoreOnEngineTests
{
    private static readonly Tag SeatA1 = new("seat", "a1");
    private static readonly Tag SeatA2 = new("seat", "a2");
    private static readonly Tag StudentS7 = new("student", "s7");

    protected abstract DatabaseFixture Fixture { get; }

    protected abstract TestDbContext Connect(string connectionString, params IInterceptor[] interceptors);

    private static TaggedEvent Reserved(string seat, string student, params Tag[] tags) =>
        new(new SeatReservedEvent(seat, student), tags.Length > 0 ? tags : [new Tag("seat", seat)]);

    /// <summary>
    /// Moves a tag head from a connection of its own, so the append's connection stays free.
    /// </summary>
    private static StaleTagHeadInterceptor MoveTagHeadFromAnotherConnection(
        string tag, Func<IInterceptor[], TestDbContext> openAnother) =>
        new(async (_, cancellationToken) =>
        {
            var mover = openAnother([]);
            await mover.Database.ExecuteSqlRawAsync(
                "UPDATE \"DcbTagHeads\" SET \"Token\" = {0} WHERE \"Tag\" = {1}",
                [Guid.NewGuid(), tag], cancellationToken);
        });

    /// <summary>
    /// Runs the body against a fresh database, with a second context onto the same one.
    /// </summary>
    private async Task WithDatabase(Func<TestDbContext, Func<IInterceptor[], TestDbContext>, Task> body)
    {
        Assert.True(Fixture.IsAvailable, Fixture.UnavailableReason);

        TypeBindings.EventTypeBindings = new Dictionary<string, Type>
        {
            { "SeatReserved:1", typeof(SeatReservedEvent) },
            { "SeatReleased:1", typeof(SeatReleasedEvent) }
        };

        DcbTypeBindings.AggregateTypeBindings = new Dictionary<string, Type>
        {
            { "Seat:1", typeof(SeatAggregate) }
        };

        DcbTypeBindings.ProjectionTypeBindings = new Dictionary<string, Type>
        {
            { "SeatSummary:1", typeof(SeatSummaryProjection) }
        };

        var connectionString = Fixture.ConnectionStringForFreshDatabase();
        var opened = new List<TestDbContext>();

        var dbContext = Connect(connectionString);
        opened.Add(dbContext);

        try
        {
            await dbContext.Database.EnsureCreatedAsync();

            await body(dbContext, interceptors =>
            {
                var other = Connect(connectionString, interceptors);
                opened.Add(other);
                return other;
            });
        }
        finally
        {
            try
            {
                await dbContext.Database.EnsureDeletedAsync();
            }
            catch
            {
                // The container is discarded after the run; cleanup must not mask the result.
            }

            foreach (var context in opened)
            {
                await context.DisposeAsync();
            }
        }
    }

    [RequiresDockerFact]
    public Task AnAppendConditionedOnTheCurrentPositionSucceeds() =>
        WithDatabase(async (dbContext, _) =>
        {
            await dbContext.SaveEvents([Reserved("a1", "s7")], condition: null);
            var position = await dbContext.GetLatestPosition(TagQuery.AnyOf(SeatA1));

            var result = await dbContext.SaveEvents([Reserved("a1", "s8")],
                new AppendCondition(TagQuery.AnyOf(SeatA1), position));

            result.IsSuccess.Should().BeTrue(
                result.Failure is null ? "the append should succeed" : result.Failure.Description);
        });

    [RequiresDockerFact]
    public Task AnAppendConditionedOnAStalePositionIsRefused() =>
        WithDatabase(async (dbContext, _) =>
        {
            await dbContext.SaveEvents([Reserved("a1", "s7")], condition: null);

            var result = await dbContext.SaveEvents([Reserved("a1", "s8")],
                AppendCondition.NothingAppendedFor(TagQuery.AnyOf(SeatA1)));

            result.IsNotSuccess.Should().BeTrue();
            result.Failure!.Type.Should().Be(EventSourcing.StoreFailures.ConcurrencyConflictType);
            dbContext.DcbEvents.Count().Should().Be(1, "a refused append leaves no trace");
        });

    [RequiresDockerFact]
    public Task TwoAppendsOnDisjointBoundariesBothSucceed() =>
        WithDatabase(async (dbContext, openAnother) =>
        {
            var other = openAnother([]);

            var first = TagQuery.AnyOf(SeatA1);
            var second = TagQuery.AnyOf(SeatA2);

            var firstPosition = await dbContext.GetLatestPosition(first);
            var secondPosition = await other.GetLatestPosition(second);

            var firstResult = await dbContext.SaveEvents([Reserved("a1", "s7")],
                new AppendCondition(first, firstPosition));
            var secondResult = await other.SaveEvents([Reserved("a2", "s8")],
                new AppendCondition(second, secondPosition));

            firstResult.IsSuccess.Should().BeTrue();
            secondResult.IsSuccess.Should().BeTrue(
                "a boundary over seat:a2 is untouched by an append to seat:a1");
        });

    [RequiresDockerFact]
    public Task TwoAppendsOnOverlappingBoundariesCannotBothSucceed() =>
        WithDatabase(async (dbContext, openAnother) =>
        {
            var other = openAnother([]);
            var boundary = TagQuery.AnyOf(SeatA1, StudentS7);

            var firstPosition = await dbContext.GetLatestPosition(boundary);
            var secondPosition = await other.GetLatestPosition(boundary);

            var firstResult = await dbContext.SaveEvents([Reserved("a1", "s7", SeatA1, StudentS7)],
                new AppendCondition(boundary, firstPosition));
            var secondResult = await other.SaveEvents([Reserved("a1", "s8", SeatA1, StudentS7)],
                new AppendCondition(boundary, secondPosition));

            firstResult.IsSuccess.Should().BeTrue();
            secondResult.IsNotSuccess.Should().BeTrue();
            dbContext.DcbEvents.Count().Should().Be(1);
        });

    [RequiresDockerFact]
    public Task AConditionedAppendIsRefusedWhenItsTagHeadMovesMidAppend() =>
        WithDatabase(async (dbContext, openAnother) =>
        {
            // The window the MAX pre-check cannot see, and the only place the token does any work.
            // Removing IsConcurrencyToken() from the model fails this on SQLite; it has to fail here
            // too, because the guard reaches the engine as provider-specific SQL.
            //
            // The tag head is moved from a connection of its own. The conditioned path has only read
            // that row at this point, so nothing is holding it, and Npgsql refuses a second statement
            // on the connection the append is using.
            var interceptor = MoveTagHeadFromAnotherConnection(SeatA1.ToString(), openAnother);
            var racing = openAnother([interceptor]);

            var result = await racing.SaveEvents([Reserved("a1", "s7")],
                AppendCondition.NothingAppendedFor(TagQuery.AnyOf(SeatA1)));

            interceptor.Fired.Should().BeTrue("the race must actually have been simulated");
            interceptor.SimulationFailure.Should().BeNull("a broken simulation proves nothing");
            result.IsNotSuccess.Should().BeTrue(
                "the tag head moved under the append, which only the concurrency token can detect");
            result.Failure!.Type.Should().Be(EventSourcing.StoreFailures.ConcurrencyConflictType);
        });

    [RequiresDockerFact]
    public Task TwoUnconditionalAppendsOverOneTagBothSucceed() =>
        WithDatabase(async (dbContext, openAnother) =>
        {
            // The unconditional path replaces the tag head token through ExecuteUpdate, which carries
            // no guard. Neither append read anything, so neither has anything to be invalidated and
            // failing either would be a conflict nobody asked for.
            var other = openAnother([]);

            var first = await dbContext.SaveEvents([Reserved("a1", "s7")], condition: null);
            var second = await other.SaveEvents([Reserved("a1", "s8")], condition: null);

            first.IsSuccess.Should().BeTrue();
            second.IsSuccess.Should().BeTrue(
                second.Failure is null ? "both should succeed" : second.Failure.Description);
            dbContext.DcbEvents.Count().Should().Be(2);
        });

    [RequiresDockerFact]
    public Task ATagAtItsFullWidthSavesAndReadsBack() =>
        WithDatabase(async (dbContext, _) =>
        {
            // DcbEventTags is keyed (Tag, Position): nvarchar(255) is 510 bytes plus 8 for the
            // bigint, against SQL Server's 900-byte limit for an index key. 1.7.0 removed the last
            // table that broke that limit, and this store must not reintroduce it. Asserting the
            // arithmetic in the model proves nothing about what the engine accepts.
            var widest = new Tag("seat", new string('a', 255 - "seat:".Length));
            widest.ToString().Length.Should().Be(255);

            var result = await dbContext.SaveEvents(
                [new TaggedEvent(new SeatReservedEvent("a1", "s7"), [widest])],
                AppendCondition.NothingAppendedFor(TagQuery.AnyOf(widest)));

            result.IsSuccess.Should().BeTrue(
                result.Failure is null ? "the append should succeed" : result.Failure.Description);

            (await dbContext.GetEvents(TagQuery.AnyOf(widest))).Should().ContainSingle();
        });

    [RequiresDockerFact]
    public Task TagsDifferingOnlyInCaseAreDifferentBoundaries() =>
        WithDatabase(async (dbContext, _) =>
        {
            // The whole point of pinning the collation. Under a case-insensitive column these two
            // boundaries would be one, and the second append would be refused.
            await dbContext.SaveEvents(
                [new TaggedEvent(new SeatReservedEvent("a1", "s7"), [new Tag("seat", "a1")])],
                condition: null);

            var upper = TagQuery.AnyOf(new Tag("seat", "A1"));

            (await dbContext.GetLatestPosition(upper)).Should().Be(AppendCondition.NoEvents,
                "seat:A1 is a different tag from seat:a1");

            var result = await dbContext.SaveEvents(
                [new TaggedEvent(new SeatReservedEvent("A1", "s8"), [new Tag("seat", "A1")])],
                AppendCondition.NothingAppendedFor(upper));

            result.IsSuccess.Should().BeTrue(
                result.Failure is null ? "the append should succeed" : result.Failure.Description);
        });
}

[Trait("Category", "Container")]
[Collection(SqlServerCollection.Name)]
public class SqlServerDcbStoreTests(SqlServerFixture fixture) : DcbStoreOnEngineTests
{
    protected override DatabaseFixture Fixture => fixture;

    protected override TestDbContext Connect(string connectionString, params IInterceptor[] interceptors) =>
        DcbStoreSchema.OnSqlServer(connectionString, interceptors);
}

[Trait("Category", "Container")]
[Collection(PostgreSqlCollection.Name)]
public class PostgreSqlDcbStoreTests(PostgreSqlFixture fixture) : DcbStoreOnEngineTests
{
    protected override DatabaseFixture Fixture => fixture;

    protected override TestDbContext Connect(string connectionString, params IInterceptor[] interceptors) =>
        DcbStoreSchema.OnPostgreSql(connectionString, interceptors);
}
