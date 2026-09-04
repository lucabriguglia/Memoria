using FluentAssertions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Tests.Models;
using Xunit;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Tests.Features;

/// <summary>
/// The read an append condition is built from.
/// </summary>
public class GetLatestPositionTests : TestBase
{
    private static readonly Tag SeatA1 = new("seat", "a1");
    private static readonly Tag SeatA2 = new("seat", "a2");
    private static readonly Tag StudentS7 = new("student", "s7");

    [Fact]
    public async Task An_empty_boundary_is_at_the_no_events_position()
    {
        var position = await Context.GetLatestPosition(TagQuery.AnyOf(SeatA1));

        position.Should().Be(AppendCondition.NoEvents);
    }

    [Fact]
    public async Task The_latest_position_is_the_highest_inside_the_boundary()
    {
        await Seed(1, new SeatReservedEvent("a1", "s7"), SeatA1.ToString());
        await Seed(5, new SeatReservedEvent("a1", "s8"), SeatA1.ToString());

        var position = await Context.GetLatestPosition(TagQuery.AnyOf(SeatA1));

        position.Should().Be(5);
    }

    [Fact]
    public async Task Events_outside_the_boundary_do_not_move_it()
    {
        // The whole point of a dynamic boundary: an unrelated append must not invalidate this one.
        await Seed(1, new SeatReservedEvent("a1", "s7"), SeatA1.ToString());
        await Seed(9, new SeatReservedEvent("a2", "s8"), SeatA2.ToString());

        var position = await Context.GetLatestPosition(TagQuery.AnyOf(SeatA1));

        position.Should().Be(1);
    }

    [Fact]
    public async Task The_event_type_filter_narrows_the_boundary()
    {
        await Seed(1, new SeatReservedEvent("a1", "s7"), SeatA1.ToString());
        await Seed(2, new SeatReleasedEvent("a1"), SeatA1.ToString());

        var position = await Context.GetLatestPosition(TagQuery.AnyOf(SeatA1), [typeof(SeatReservedEvent)]);

        position.Should().Be(1);
    }

    /// <summary>
    /// A union is at the highest position carrying <em>any</em> of its tags, not the highest carrying
    /// the first one.
    /// </summary>
    /// <remarks>
    /// The single-tag cases above cannot see the difference, and neither can the intersection cases,
    /// which take the other branch of the query builder entirely. Without this, a union that read
    /// only its first tag would satisfy every other test here — and every append conditioned on it
    /// would be accepted against a boundary that had already moved under one of its other tags.
    /// </remarks>
    [Fact]
    public async Task A_union_is_at_the_highest_position_across_all_its_tags()
    {
        await Seed(1, new SeatReservedEvent("a1", "s7"), SeatA1.ToString());
        await Seed(7, new SeatReservedEvent("a2", "s8"), SeatA2.ToString());
        await Seed(9, new SeatReservedEvent("a3", "s9"), "seat:a3");

        var position = await Context.GetLatestPosition(TagQuery.AnyOf(SeatA1, SeatA2));

        position.Should().Be(7, "seat:a2 is inside the boundary and seat:a3 is not");
    }

    [Fact]
    public async Task An_intersection_boundary_is_at_the_highest_position_carrying_all_its_tags()
    {
        await Seed(1, new SeatReservedEvent("a1", "s7"), SeatA1.ToString(), StudentS7.ToString());
        await Seed(9, new SeatReservedEvent("a1", "s8"), SeatA1.ToString());

        var position = await Context.GetLatestPosition(TagQuery.AllOf(SeatA1, StudentS7));

        position.Should().Be(1);
    }

    [Fact]
    public async Task An_intersection_of_tags_that_never_meet_is_empty()
    {
        // Both tags have events; no event has both. A boundary that reported position 2 here would
        // be a union wearing an intersection's name, and every append conditioned on it would be
        // refused by events it does not depend on.
        await Seed(1, new SeatReservedEvent("a1", "s8"), SeatA1.ToString());
        await Seed(2, new SeatReservedEvent("a2", "s7"), SeatA2.ToString(), StudentS7.ToString());

        var position = await Context.GetLatestPosition(TagQuery.AllOf(SeatA1, StudentS7));

        position.Should().Be(AppendCondition.NoEvents);
    }
}
