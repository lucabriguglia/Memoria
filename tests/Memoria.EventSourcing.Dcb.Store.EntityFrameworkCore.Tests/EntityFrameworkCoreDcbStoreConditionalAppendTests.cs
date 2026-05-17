using FluentAssertions;
using Xunit;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Tests;

public class EntityFrameworkCoreDcbStoreConditionalAppendTests : TestBase
{
    private static QueryItem SeatItem(string seat) =>
        new([typeof(SeatReserved), typeof(SeatReleased)], TagSet.Of(new Tag("seat", seat)));

    [Fact]
    public async Task Append_WithCondition_Succeeds_WhenNoMatchingEventAppearedAfterCapturedPosition()
    {
        await Store.Append([EventToAppend.Of(new SeatReserved("A12"), new Tag("seat", "A12"))]);

        var query = DcbQuery.Of(SeatItem("A13"));
        var read = await Store.Query(query);
        var capturedPosition = read.Value!.Count == 0 ? 0L : read.Value[^1].GlobalPosition;
        capturedPosition.Should().Be(0L);

        var append = await Store.Append(
            [EventToAppend.Of(new SeatReserved("A13"), new Tag("seat", "A13"))],
            new AppendCondition(query, capturedPosition));

        append.IsSuccess.Should().BeTrue();
        append.Value!.Appended[0].GlobalPosition.Should().Be(2L);
    }

    [Fact]
    public async Task Append_WithCondition_ReturnsConcurrencyConflict_WhenMatchingEventAppearedAfterCapturedPosition()
    {
        var query = DcbQuery.Of(SeatItem("A12"));

        var read = await Store.Query(query);
        var capturedPosition = read.Value!.Count == 0 ? 0L : read.Value[^1].GlobalPosition;

        await Store.Append([EventToAppend.Of(new SeatReserved("A12"), new Tag("seat", "A12"))]);

        var append = await Store.Append(
            [EventToAppend.Of(new SeatReserved("A12"), new Tag("seat", "A12"))],
            new AppendCondition(query, capturedPosition));

        append.IsSuccess.Should().BeFalse();
        var conflict = append.Failure.Should().BeOfType<ConcurrencyConflict>().Subject;
        conflict.ExpectedAfterPosition.Should().Be(0L);
        conflict.ObservedPosition.Should().Be(1L);
    }

    [Fact]
    public async Task Append_WithCondition_Ignores_NonMatchingEventsAppearedAfterCapturedPosition()
    {
        var queryForSeatA12 = DcbQuery.Of(SeatItem("A12"));

        await Store.Append([EventToAppend.Of(new SeatReserved("A99"), new Tag("seat", "A99"))]);

        var append = await Store.Append(
            [EventToAppend.Of(new SeatReserved("A12"), new Tag("seat", "A12"))],
            new AppendCondition(queryForSeatA12, AfterPosition: 0L));

        append.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Append_WithCondition_ChecksAgainstAllQueryItems()
    {
        var query = DcbQuery.Of(
            SeatItem("A12"),
            new QueryItem([typeof(PaymentTaken)], TagSet.Of(new Tag("orderId", "O-7"))));

        await Store.Append([EventToAppend.Of(new PaymentTaken("O-7", 50m), new Tag("orderId", "O-7"))]);

        var append = await Store.Append(
            [EventToAppend.Of(new SeatReserved("A12"), new Tag("seat", "A12"))],
            new AppendCondition(query, AfterPosition: 0L));

        append.Failure.Should().BeOfType<ConcurrencyConflict>()
            .Which.ObservedPosition.Should().Be(1L);
    }
}
