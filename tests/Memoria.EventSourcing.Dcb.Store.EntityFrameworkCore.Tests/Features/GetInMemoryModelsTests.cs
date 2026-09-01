using FluentAssertions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Tests.Models;
using Xunit;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Tests.Features;

/// <summary>
/// Folding a boundary into a model without persisting a snapshot.
/// </summary>
public class GetInMemoryModelsTests : TestBase
{
    private static readonly Tag SeatA1 = new("seat", "a1");
    private static readonly Tag SeatA2 = new("seat", "a2");

    [Fact]
    public async Task An_aggregate_folds_the_events_inside_the_boundary()
    {
        await Seed(1, new SeatReservedEvent("a1", "s7"), SeatA1.ToString());
        await Seed(2, new SeatReleasedEvent("a1"), SeatA1.ToString());

        var result = await Context.GetInMemoryAggregate(new SeatId("a1"));

        result.IsSuccess.Should().BeTrue();
        result.Value!.ReservedBy.Should().BeNull();
        result.Value.Reservations.Should().Be(1);
        result.Value.Version.Should().Be(2);
    }

    [Fact]
    public async Task An_aggregate_is_identified_and_positioned_after_folding()
    {
        await Seed(4, new SeatReservedEvent("a1", "s7"), SeatA1.ToString());

        var result = await Context.GetInMemoryAggregate(new SeatId("a1"));

        result.Value!.AggregateId.Should().Be("a1:1", "the store id carries the [AggregateType] version");
        result.Value.LatestPosition.Should().Be(4, "the fold reached position 4");
    }

    [Fact]
    public async Task An_empty_boundary_folds_into_an_untouched_aggregate()
    {
        var result = await Context.GetInMemoryAggregate(new SeatId("a1"));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Version.Should().Be(0);
        result.Value.ReservedBy.Should().BeNull();
    }

    [Fact]
    public async Task An_aggregates_own_event_type_filter_is_applied()
    {
        // CourseRenamed carries the seat tag but is not in SeatAggregate.EventTypeFilter.
        await Seed(1, new SeatReservedEvent("a1", "s7"), SeatA1.ToString());
        await Seed(2, new CourseRenamedEvent("c1", "Renamed"), SeatA1.ToString());

        var result = await Context.GetInMemoryAggregate(new SeatId("a1"));

        result.Value!.Version.Should().Be(1);
    }

    [Fact]
    public async Task Events_outside_the_boundary_are_not_folded()
    {
        await Seed(1, new SeatReservedEvent("a1", "s7"), SeatA1.ToString());
        await Seed(2, new SeatReservedEvent("a2", "s8"), SeatA2.ToString());

        var result = await Context.GetInMemoryAggregate(new SeatId("a1"));

        result.Value!.Reservations.Should().Be(1);
    }

    [Fact]
    public async Task An_aggregate_can_be_folded_up_to_a_position()
    {
        await Seed(1, new SeatReservedEvent("a1", "s7"), SeatA1.ToString());
        await Seed(2, new SeatReleasedEvent("a1"), SeatA1.ToString());

        var result = await Context.GetInMemoryAggregate(new SeatId("a1"), upToPosition: 1);

        result.Value!.ReservedBy.Should().Be("s7", "the release at position 2 is excluded");
    }

    [Fact]
    public async Task An_aggregate_can_be_folded_up_to_a_date()
    {
        var day = new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.Zero);
        await Seed(1, new SeatReservedEvent("a1", "s7"), day, SeatA1.ToString());
        await Seed(2, new SeatReleasedEvent("a1"), day.AddDays(2), SeatA1.ToString());

        var result = await Context.GetInMemoryAggregate(new SeatId("a1"),
            upToDate: day.AddDays(1));

        result.Value!.ReservedBy.Should().Be("s7");
    }

    [Fact]
    public async Task A_projection_folds_the_events_inside_the_boundary()
    {
        await Seed(1, new SeatReservedEvent("a1", "s7"), SeatA1.ToString());
        await Seed(2, new SeatReleasedEvent("a1"), SeatA1.ToString());

        var result = await Context.GetInMemoryProjection(new SeatSummaryId("a1"));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Reservations.Should().Be(1, "the projection filters to reservations only");
        result.Value.ProjectionId.Should().Be("a1:1");
    }

    [Fact]
    public async Task A_projection_can_be_folded_up_to_a_position_and_a_date()
    {
        var day = new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.Zero);
        await Seed(1, new SeatReservedEvent("a1", "s7"), day, SeatA1.ToString());
        await Seed(2, new SeatReservedEvent("a1", "s8"), day.AddDays(2), SeatA1.ToString());

        (await Context.GetInMemoryProjection(new SeatSummaryId("a1"), upToPosition: 1))
            .Value!.Reservations.Should().Be(1);

        (await Context.GetInMemoryProjection(new SeatSummaryId("a1"), upToDate: day.AddDays(1)))
            .Value!.Reservations.Should().Be(1);
    }
}
