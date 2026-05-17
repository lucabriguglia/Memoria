using FluentAssertions;
using Memoria.EventSourcing.Domain;
using Memoria.Results;
using Xunit;

namespace Memoria.EventSourcing.Dcb.Tests;

public class InMemoryDcbStoreConditionalAppendTests
{
    private sealed record SeatReserved(string Seat) : IEvent;
    private sealed record SeatReleased(string Seat) : IEvent;

    private static QueryItem SeatItem(string seat) =>
        new([typeof(SeatReserved), typeof(SeatReleased)], TagSet.Of(new Tag("seat", seat)));

    [Fact]
    public async Task Append_WithCondition_Succeeds_WhenNoMatchingEventAppearedAfterCapturedPosition()
    {
        var store = new InMemoryDcbStore();
        await store.Append([EventToAppend.Of(new SeatReserved("A12"), new Tag("seat", "A12"))]);

        var query = DcbQuery.Of(SeatItem("A13"));
        var read = await store.Query(query);
        var capturedPosition = read.Value!.Count == 0 ? 0L : read.Value[^1].GlobalPosition;

        var append = await store.Append(
            [EventToAppend.Of(new SeatReserved("A13"), new Tag("seat", "A13"))],
            new AppendCondition(query, capturedPosition));

        append.IsSuccess.Should().BeTrue();
        append.Value!.Appended.Should().ContainSingle();
        append.Value.Appended[0].GlobalPosition.Should().Be(2);
    }

    [Fact]
    public async Task Append_WithCondition_ReturnsConcurrencyConflict_WhenMatchingEventAppearedAfterCapturedPosition()
    {
        var store = new InMemoryDcbStore();
        var query = DcbQuery.Of(SeatItem("A12"));

        var read = await store.Query(query);
        var capturedPosition = read.Value!.Count == 0 ? 0L : read.Value[^1].GlobalPosition;
        capturedPosition.Should().Be(0L);

        await store.Append([EventToAppend.Of(new SeatReserved("A12"), new Tag("seat", "A12"))]);

        var append = await store.Append(
            [EventToAppend.Of(new SeatReserved("A12"), new Tag("seat", "A12"))],
            new AppendCondition(query, capturedPosition));

        append.IsSuccess.Should().BeFalse();
        append.Failure.Should().BeOfType<ConcurrencyConflict>()
            .Which.ObservedPosition.Should().Be(1L);
    }

    [Fact]
    public async Task Append_WithCondition_Ignores_NonMatchingEventsAppearedAfterCapturedPosition()
    {
        var store = new InMemoryDcbStore();
        var queryForSeatA12 = DcbQuery.Of(SeatItem("A12"));
        var capturedPosition = 0L;

        await store.Append([EventToAppend.Of(new SeatReserved("A99"), new Tag("seat", "A99"))]);

        var append = await store.Append(
            [EventToAppend.Of(new SeatReserved("A12"), new Tag("seat", "A12"))],
            new AppendCondition(queryForSeatA12, capturedPosition));

        append.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ConcurrencyConflict_CarriesTheQueryAndExpectedPosition()
    {
        var store = new InMemoryDcbStore();
        var query = DcbQuery.Of(SeatItem("A12"));
        await store.Append([EventToAppend.Of(new SeatReserved("A12"), new Tag("seat", "A12"))]);

        var append = await store.Append(
            [EventToAppend.Of(new SeatReserved("A12"), new Tag("seat", "A12"))],
            new AppendCondition(query, 0L));

        var conflict = append.Failure.Should().BeOfType<ConcurrencyConflict>().Subject;
        conflict.Query.Should().BeSameAs(query);
        conflict.ExpectedAfterPosition.Should().Be(0L);
        conflict.ObservedPosition.Should().Be(1L);
    }
}
