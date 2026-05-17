using FluentAssertions;
using Xunit;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Tests;

public class EntityFrameworkCoreDcbStoreQueryTests : TestBase
{
    [Fact]
    public async Task Query_ReturnsOnlyEventsMatchingTypeFilter()
    {
        await Store.Append(
        [
            EventToAppend.Of(new SeatReserved("A12"), new Tag("seat", "A12")),
            EventToAppend.Of(new SeatReleased("A12"), new Tag("seat", "A12")),
            EventToAppend.Of(new SeatReserved("A13"), new Tag("seat", "A13"))
        ]);

        var result = await Store.Query(DcbQuery.Of(
            new QueryItem([typeof(SeatReserved)], TagSet.Empty)));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(2);
        result.Value.Should().AllSatisfy(e => e.Payload.Should().BeOfType<SeatReserved>());
    }

    [Fact]
    public async Task Query_ReturnsOnlyEventsCarryingAllRequiredTags()
    {
        await Store.Append(
        [
            EventToAppend.Of(new SeatReserved("A12"), new Tag("seat", "A12"), new Tag("showId", "S-1")),
            EventToAppend.Of(new SeatReserved("A13"), new Tag("seat", "A13"), new Tag("showId", "S-1")),
            EventToAppend.Of(new SeatReserved("A12"), new Tag("seat", "A12"), new Tag("showId", "S-2"))
        ]);

        var result = await Store.Query(DcbQuery.Of(
            new QueryItem([], TagSet.Of(new Tag("seat", "A12"), new Tag("showId", "S-1")))));

        result.Value!.Should().ContainSingle()
            .Which.Payload.Should().BeOfType<SeatReserved>()
            .Which.Seat.Should().Be("A12");
    }

    [Fact]
    public async Task Query_ReturnsResultsOrderedByGlobalPosition()
    {
        await Store.Append([EventToAppend.Of(new SeatReserved("A12"), new Tag("seat", "A12"))]);
        await Store.Append([EventToAppend.Of(new SeatReleased("A12"), new Tag("seat", "A12"))]);
        await Store.Append([EventToAppend.Of(new SeatReserved("A12"), new Tag("seat", "A12"))]);

        var result = await Store.Query(DcbQuery.Of(
            new QueryItem([], TagSet.Of(new Tag("seat", "A12")))));

        result.Value!.Select(e => e.GlobalPosition).Should().Equal(1L, 2L, 3L);
    }

    [Fact]
    public async Task Query_OrsAcrossQueryItems()
    {
        await Store.Append(
        [
            EventToAppend.Of(new SeatReserved("A12"), new Tag("seat", "A12")),
            EventToAppend.Of(new PaymentTaken("O-7", 50m), new Tag("orderId", "O-7"))
        ]);

        var result = await Store.Query(DcbQuery.Of(
            new QueryItem([typeof(SeatReserved)], TagSet.Of(new Tag("seat", "A12"))),
            new QueryItem([typeof(PaymentTaken)], TagSet.Of(new Tag("orderId", "O-7")))));

        result.Value!.Should().HaveCount(2);
    }

    [Fact]
    public async Task Query_EmptyQuery_ReturnsNothing()
    {
        await Store.Append([EventToAppend.Of(new SeatReserved("A12"), new Tag("seat", "A12"))]);

        var result = await Store.Query(DcbQuery.Of());

        result.Value!.Should().BeEmpty();
    }
}
