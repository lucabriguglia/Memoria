using FluentAssertions;
using Memoria.EventSourcing.Domain;
using Memoria.Results;
using Xunit;

namespace Memoria.EventSourcing.Dcb.Tests;

public class DecideTests
{
    private sealed record SeatReserved(string Seat) : IEvent;
    private sealed record SeatReleased(string Seat) : IEvent;

    private sealed record SeatState(bool Taken)
    {
        public static SeatState Empty { get; } = new(Taken: false);

        public SeatState Apply(IEvent evt) => evt switch
        {
            SeatReserved => this with { Taken = true },
            SeatReleased => this with { Taken = false },
            _ => this
        };
    }

    private static DcbQuery SeatQuery(string seat) => DcbQuery.Of(
        new QueryItem([typeof(SeatReserved), typeof(SeatReleased)], TagSet.Of(new Tag("seat", seat))));

    [Fact]
    public async Task Decide_AppendsEmittedEvents_WhenStateAllowsIt()
    {
        var store = new InMemoryDcbStore();

        var result = await store.Decide(
            query: SeatQuery("A12"),
            initialState: SeatState.Empty,
            fold: (state, evt) => state.Apply(evt),
            decide: state => state.Taken
                ? Result<IReadOnlyList<EventToAppend>>.Fail(new Failure(Title: "Seat taken"))
                : Result<IReadOnlyList<EventToAppend>>.Ok([EventToAppend.Of(new SeatReserved("A12"), new Tag("seat", "A12"))]));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Appended.Should().ContainSingle()
            .Which.Payload.Should().BeOfType<SeatReserved>();
    }

    [Fact]
    public async Task Decide_PropagatesDomainRejection_WhenDecideReturnsFailure()
    {
        var store = new InMemoryDcbStore();
        await store.Append([EventToAppend.Of(new SeatReserved("A12"), new Tag("seat", "A12"))]);

        var result = await store.Decide(
            query: SeatQuery("A12"),
            initialState: SeatState.Empty,
            fold: (state, evt) => state.Apply(evt),
            decide: state => state.Taken
                ? Result<IReadOnlyList<EventToAppend>>.Fail(new Failure(Title: "Seat taken"))
                : Result<IReadOnlyList<EventToAppend>>.Ok([EventToAppend.Of(new SeatReserved("A12"), new Tag("seat", "A12"))]));

        result.IsSuccess.Should().BeFalse();
        result.Failure!.Title.Should().Be("Seat taken");
    }

    [Fact]
    public async Task Decide_AppendsNothing_WhenDecideReturnsEmptyList()
    {
        var store = new InMemoryDcbStore();
        await store.Append([EventToAppend.Of(new SeatReserved("A12"), new Tag("seat", "A12"))]);

        var result = await store.Decide<SeatState>(
            query: SeatQuery("A12"),
            initialState: SeatState.Empty,
            fold: (state, evt) => state.Apply(evt),
            decide: _ => Result<IReadOnlyList<EventToAppend>>.Ok([]));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Appended.Should().BeEmpty();
        result.Value.LastPosition.Should().Be(1L, "the helper captures the read position even when nothing is appended");
    }

    [Fact]
    public async Task Decide_PropagatesConcurrencyConflict_WhenMatchingEventLandsBetweenReadAndAppend()
    {
        var store = new InMemoryDcbStore();

        var result = await store.Decide(
            query: SeatQuery("A12"),
            initialState: SeatState.Empty,
            fold: (state, evt) => state.Apply(evt),
            decide: _ =>
            {
                // Simulate a competing writer landing a matching event between read and append.
                store.Append([EventToAppend.Of(new SeatReserved("A12"), new Tag("seat", "A12"))]).GetAwaiter().GetResult();
                return Result<IReadOnlyList<EventToAppend>>.Ok([EventToAppend.Of(new SeatReserved("A12"), new Tag("seat", "A12"))]);
            });

        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().BeOfType<ConcurrencyConflict>();
    }

    [Fact]
    public async Task Decide_FoldSeesEveryQueriedEvent_InOrder()
    {
        var store = new InMemoryDcbStore();
        await store.Append([EventToAppend.Of(new SeatReserved("A12"), new Tag("seat", "A12"))]);
        await store.Append([EventToAppend.Of(new SeatReleased("A12"), new Tag("seat", "A12"))]);
        await store.Append([EventToAppend.Of(new SeatReserved("A12"), new Tag("seat", "A12"))]);

        var seen = new List<IEvent>();
        var result = await store.Decide<SeatState>(
            query: SeatQuery("A12"),
            initialState: SeatState.Empty,
            fold: (state, evt) =>
            {
                seen.Add(evt);
                return state.Apply(evt);
            },
            decide: _ => Result<IReadOnlyList<EventToAppend>>.Ok([]));

        result.IsSuccess.Should().BeTrue();
        seen.Select(e => e.GetType()).Should().Equal(typeof(SeatReserved), typeof(SeatReleased), typeof(SeatReserved));
    }
}
