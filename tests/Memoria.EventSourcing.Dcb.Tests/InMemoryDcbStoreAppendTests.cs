using FluentAssertions;
using Memoria.EventSourcing.Domain;
using Xunit;

namespace Memoria.EventSourcing.Dcb.Tests;

public class InMemoryDcbStoreAppendTests
{
    private sealed record SeatReserved(string Seat) : IEvent;
    private sealed record SeatReleased(string Seat) : IEvent;

    [Fact]
    public async Task Append_AssignsMonotonicGlobalPositionsStartingAtOne()
    {
        var store = new InMemoryDcbStore();

        var append = await store.Append(
        [
            EventToAppend.Of(new SeatReserved("A12"), new Tag("seat", "A12")),
            EventToAppend.Of(new SeatReserved("A13"), new Tag("seat", "A13"))
        ]);

        append.IsSuccess.Should().BeTrue();
        var appended = append.Value!.Appended;
        appended.Should().HaveCount(2);
        appended[0].GlobalPosition.Should().Be(1);
        appended[1].GlobalPosition.Should().Be(2);
        append.Value.LastPosition.Should().Be(2);
    }

    [Fact]
    public async Task Append_AssignsEventIds()
    {
        var store = new InMemoryDcbStore();

        var append = await store.Append([EventToAppend.Of(new SeatReserved("A12"), new Tag("seat", "A12"))]);

        append.Value!.Appended[0].EventId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Query_ReturnsOnlyEventsMatchingTypeFilter()
    {
        var store = new InMemoryDcbStore();
        await store.Append(
        [
            EventToAppend.Of(new SeatReserved("A12"), new Tag("seat", "A12")),
            EventToAppend.Of(new SeatReleased("A12"), new Tag("seat", "A12")),
            EventToAppend.Of(new SeatReserved("A13"), new Tag("seat", "A13"))
        ]);

        var result = await store.Query(DcbQuery.Of(
            new QueryItem([typeof(SeatReserved)], TagSet.Empty)));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(2);
        result.Value.Should().AllSatisfy(e => e.Payload.Should().BeOfType<SeatReserved>());
    }

    [Fact]
    public async Task Query_ReturnsOnlyEventsMatchingTagFilter()
    {
        var store = new InMemoryDcbStore();
        await store.Append(
        [
            EventToAppend.Of(new SeatReserved("A12"), new Tag("seat", "A12")),
            EventToAppend.Of(new SeatReserved("A13"), new Tag("seat", "A13"))
        ]);

        var result = await store.Query(DcbQuery.Of(
            new QueryItem([], TagSet.Of(new Tag("seat", "A12")))));

        result.Value!.Should().ContainSingle()
            .Which.Payload.Should().BeOfType<SeatReserved>()
            .Which.Seat.Should().Be("A12");
    }

    [Fact]
    public async Task Query_ReturnsResultsOrderedByGlobalPosition()
    {
        var store = new InMemoryDcbStore();
        await store.Append([EventToAppend.Of(new SeatReserved("A12"), new Tag("seat", "A12"))]);
        await store.Append([EventToAppend.Of(new SeatReleased("A12"), new Tag("seat", "A12"))]);
        await store.Append([EventToAppend.Of(new SeatReserved("A12"), new Tag("seat", "A12"))]);

        var result = await store.Query(DcbQuery.Of(
            new QueryItem([], TagSet.Of(new Tag("seat", "A12")))));

        result.Value!.Select(e => e.GlobalPosition).Should().Equal(1L, 2L, 3L);
    }

    [Fact]
    public async Task Append_EmptyEventsList_IsNoOp()
    {
        var store = new InMemoryDcbStore();

        var append = await store.Append([]);

        append.IsSuccess.Should().BeTrue();
        append.Value!.Appended.Should().BeEmpty();
        append.Value.LastPosition.Should().Be(0);
    }
}
