using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Npgsql.Tests;

[Collection(PostgresCollection.Name)]
public class NpgsqlDcbStoreTests(PostgresFixture postgres) : IAsyncLifetime
{
    private TestDbContext _dbContext = null!;
    private NpgsqlDcbStore _store = null!;

    public async Task InitializeAsync()
    {
        _dbContext = postgres.CreateDbContext();
        // Each test starts from an empty log.
        await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE dcb_event_tags, dcb_events;");
        _store = new NpgsqlDcbStore(_dbContext);
    }

    public Task DisposeAsync() => _dbContext.DisposeAsync().AsTask();

    [Fact]
    public async Task Append_then_Query_round_trips_events_with_tags()
    {
        await _store.Append([EventToAppend.Of(new SeatReserved("A12"), new Tag("seat", "A12"))]);

        var result = await _store.Query(DcbQuery.Of(
            new QueryItem([], TagSet.Of(new Tag("seat", "A12")))));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().ContainSingle()
            .Which.Payload.Should().BeOfType<SeatReserved>()
            .Which.Seat.Should().Be("A12");
    }

    [Fact]
    public async Task Conditional_Append_succeeds_when_no_matching_event_appeared_after_captured_position()
    {
        var query = DcbQuery.Of(new QueryItem([typeof(SeatReserved)], TagSet.Of(new Tag("seat", "A12"))));
        var read = await _store.Query(query);
        var capturedPosition = read.Value!.Count == 0 ? 0L : read.Value[^1].GlobalPosition;

        var append = await _store.Append(
            [EventToAppend.Of(new SeatReserved("A12"), new Tag("seat", "A12"))],
            new AppendCondition(query, capturedPosition));

        append.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Conditional_Append_returns_ConcurrencyConflict_when_a_matching_event_was_written_in_between()
    {
        var query = DcbQuery.Of(new QueryItem([typeof(SeatReserved)], TagSet.Of(new Tag("seat", "A12"))));
        await _store.Append([EventToAppend.Of(new SeatReserved("A12"), new Tag("seat", "A12"))]);

        var append = await _store.Append(
            [EventToAppend.Of(new SeatReserved("A12"), new Tag("seat", "A12"))],
            new AppendCondition(query, 0L));

        append.Failure.Should().BeOfType<ConcurrencyConflict>()
            .Which.ObservedPosition.Should().Be(1L);
    }
}
