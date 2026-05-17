using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Npgsql.Tests;

[Collection(PostgresCollection.Name)]
public class NpgsqlDcbStoreConcurrencyTests(PostgresFixture postgres) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        TestTypeBindings.Register();
        await using var seed = postgres.CreateDbContext();
        await seed.Database.ExecuteSqlRawAsync("TRUNCATE TABLE dcb_event_tags, dcb_events;");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Two concurrent writers, each running the read-decide-append flow for the same seat.
    /// Without advisory locks both can read an empty log and both can append — that would be
    /// a double-booking. With locks, exactly one wins and the other gets a ConcurrencyConflict.
    /// </summary>
    [Fact]
    public async Task Two_concurrent_writers_competing_for_the_same_seat_produce_exactly_one_success_and_one_conflict()
    {
        var query = DcbQuery.Of(new QueryItem(
            [typeof(SeatReserved), typeof(SeatReleased)],
            TagSet.Of(new Tag("seat", "A12"))));

        var startSignal = new TaskCompletionSource();

        async Task<bool> AttemptReserve()
        {
            await using var db = postgres.CreateDbContext();
            var store = new NpgsqlDcbStore(db);

            var read = await store.Query(query);
            var capturedPosition = read.Value!.Count == 0 ? 0L : read.Value[^1].GlobalPosition;

            // Both writers reach the append at approximately the same moment.
            await startSignal.Task;

            var result = await store.Append(
                [EventToAppend.Of(new SeatReserved("A12"), new Tag("seat", "A12"))],
                new AppendCondition(query, capturedPosition));

            return result.IsSuccess;
        }

        var taskA = Task.Run(AttemptReserve);
        var taskB = Task.Run(AttemptReserve);

        // Give both tasks a moment to reach the await point.
        await Task.Delay(50);
        startSignal.SetResult();

        var outcomes = await Task.WhenAll(taskA, taskB);

        outcomes.Count(success => success).Should().Be(1, "advisory locks must let exactly one writer win");
        outcomes.Count(success => !success).Should().Be(1, "the loser must see a ConcurrencyConflict");

        await using var verify = postgres.CreateDbContext();
        (await verify.DcbEvents.CountAsync()).Should().Be(1);
    }

    /// <summary>
    /// Different seats touch disjoint advisory-lock keys, so the two writers must NOT serialize.
    /// Both should succeed; the log ends up with two events.
    /// </summary>
    [Fact]
    public async Task Two_concurrent_writers_for_different_seats_both_succeed()
    {
        async Task<bool> AttemptReserve(string seat)
        {
            await using var db = postgres.CreateDbContext();
            var store = new NpgsqlDcbStore(db);

            var query = DcbQuery.Of(new QueryItem(
                [typeof(SeatReserved)],
                TagSet.Of(new Tag("seat", seat))));

            var read = await store.Query(query);
            var capturedPosition = read.Value!.Count == 0 ? 0L : read.Value[^1].GlobalPosition;

            var result = await store.Append(
                [EventToAppend.Of(new SeatReserved(seat), new Tag("seat", seat))],
                new AppendCondition(query, capturedPosition));

            return result.IsSuccess;
        }

        var outcomes = await Task.WhenAll(
            Task.Run(() => AttemptReserve("A12")),
            Task.Run(() => AttemptReserve("A13")));

        outcomes.Should().AllSatisfy(success => success.Should().BeTrue());

        await using var verify = postgres.CreateDbContext();
        (await verify.DcbEvents.CountAsync()).Should().Be(2);
    }
}
