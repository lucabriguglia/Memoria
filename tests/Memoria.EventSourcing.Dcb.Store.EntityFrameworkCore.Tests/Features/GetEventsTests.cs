using Memoria.EventSourcing.Domain;
using FluentAssertions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Tests.Models;
using Xunit;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Tests.Features;

public class GetEventsTests : TestBase
{
    private static readonly Tag SeatA1 = new("seat", "a1");
    private static readonly Tag SeatA2 = new("seat", "a2");
    private static readonly Tag StudentS7 = new("student", "s7");

    [Fact]
    public async Task A_boundary_returns_only_the_events_carrying_one_of_its_tags()
    {
        await Seed(1, new SeatReservedEvent("a1", "s7"), SeatA1.ToString(), StudentS7.ToString());
        await Seed(2, new SeatReservedEvent("a2", "s8"), SeatA2.ToString(), "student:s8");

        var events = await Context.GetEvents(TagQuery.AnyOf(SeatA1));

        events.Should().ContainSingle()
            .Which.Should().BeOfType<SeatReservedEvent>()
            .Which.SeatId.Should().Be("a1");
    }

    [Fact]
    public async Task A_boundary_over_several_tags_returns_the_union()
    {
        await Seed(1, new SeatReservedEvent("a1", "s7"), SeatA1.ToString());
        await Seed(2, new SeatReservedEvent("a2", "s8"), SeatA2.ToString());
        await Seed(3, new SeatReservedEvent("a3", "s9"), "seat:a3");

        var events = await Context.GetEvents(TagQuery.AnyOf(SeatA1, SeatA2));

        events.Should().HaveCount(2);
    }

    [Fact]
    public async Task An_event_matching_two_tags_of_one_boundary_is_returned_once()
    {
        // The union is over events, not over tag rows. Returning it twice would double-apply it.
        await Seed(1, new SeatReservedEvent("a1", "s7"), SeatA1.ToString(), StudentS7.ToString());

        var events = await Context.GetEvents(TagQuery.AnyOf(SeatA1, StudentS7));

        events.Should().ContainSingle();
    }

    [Fact]
    public async Task Events_come_back_in_position_order()
    {
        await Seed(3, new SeatReservedEvent("a1", "s9"), SeatA1.ToString());
        await Seed(1, new SeatReservedEvent("a1", "s7"), SeatA1.ToString());
        await Seed(2, new SeatReservedEvent("a1", "s8"), SeatA1.ToString());

        var events = await Context.GetEvents(TagQuery.AnyOf(SeatA1));

        events.Cast<SeatReservedEvent>().Select(@event => @event.StudentId)
            .Should().ContainInOrder("s7", "s8", "s9");
    }

    [Fact]
    public async Task An_empty_boundary_returns_nothing()
    {
        await Seed(1, new SeatReservedEvent("a1", "s7"), SeatA1.ToString());

        var events = await Context.GetEvents(TagQuery.AnyOf(new Tag("seat", "never-used")));

        events.Should().BeEmpty();
    }

    [Fact]
    public async Task The_event_type_filter_narrows_within_the_boundary()
    {
        await Seed(1, new SeatReservedEvent("a1", "s7"), SeatA1.ToString());
        await Seed(2, new SeatReleasedEvent("a1"), SeatA1.ToString());

        var events = await Context.GetEvents(TagQuery.AnyOf(SeatA1), [typeof(SeatReleasedEvent)]);

        events.Should().ContainSingle().Which.Should().BeOfType<SeatReleasedEvent>();
    }

    [Fact]
    public async Task Reads_can_be_bounded_by_position()
    {
        await Seed(1, new SeatReservedEvent("a1", "s7"), SeatA1.ToString());
        await Seed(2, new SeatReservedEvent("a1", "s8"), SeatA1.ToString());
        await Seed(3, new SeatReservedEvent("a1", "s9"), SeatA1.ToString());

        var query = TagQuery.AnyOf(SeatA1);

        (await Context.GetEventsFromPosition(query, 2)).Should().HaveCount(2);
        (await Context.GetEventsUpToPosition(query, 2)).Should().HaveCount(2);
        (await Context.GetEventsBetweenPositions(query, 2, 3)).Should().HaveCount(2);
    }

    [Fact]
    public async Task Position_bounds_are_inclusive()
    {
        await Seed(1, new SeatReservedEvent("a1", "s7"), SeatA1.ToString());
        await Seed(2, new SeatReservedEvent("a1", "s8"), SeatA1.ToString());

        var query = TagQuery.AnyOf(SeatA1);

        (await Context.GetEventsFromPosition(query, 2)).Should().ContainSingle();
        (await Context.GetEventsUpToPosition(query, 1)).Should().ContainSingle();
    }

    [Fact]
    public async Task Reads_can_be_bounded_by_date()
    {
        var day = new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.Zero);

        await Seed(1, new SeatReservedEvent("a1", "s7"), day, SeatA1.ToString());
        await Seed(2, new SeatReservedEvent("a1", "s8"), day.AddDays(1), SeatA1.ToString());
        await Seed(3, new SeatReservedEvent("a1", "s9"), day.AddDays(2), SeatA1.ToString());

        var query = TagQuery.AnyOf(SeatA1);

        (await Context.GetEventsFromDate(query, day.AddDays(1))).Should().HaveCount(2);
        (await Context.GetEventsUpToDate(query, day.AddDays(1))).Should().HaveCount(2);
        (await Context.GetEventsBetweenDates(query, day.AddDays(1), day.AddDays(2))).Should().HaveCount(2);
    }

    [Fact]
    public async Task An_intersection_boundary_returns_only_the_events_carrying_every_one_of_its_tags()
    {
        await Seed(1, new SeatReservedEvent("a1", "s7"), SeatA1.ToString(), StudentS7.ToString());
        await Seed(2, new SeatReservedEvent("a1", "s8"), SeatA1.ToString());
        await Seed(3, new SeatReservedEvent("a2", "s7"), SeatA2.ToString(), StudentS7.ToString());

        var events = await Context.GetEvents(TagQuery.AllOf(SeatA1, StudentS7));

        events.Should().ContainSingle()
            .Which.Should().BeOfType<SeatReservedEvent>()
            .Which.StudentId.Should().Be("s7");
    }

    [Fact]
    public async Task An_intersection_boundary_returns_an_event_carrying_more_than_it_asks_for()
    {
        await Seed(1, new SeatReservedEvent("a1", "s7"), SeatA1.ToString(), StudentS7.ToString(), "term:t3");

        var events = await Context.GetEvents(TagQuery.AllOf(SeatA1, StudentS7));

        events.Should().ContainSingle();
    }

    [Fact]
    public async Task An_intersection_boundary_returns_a_matching_event_once()
    {
        // One row per event, not per matching tag row — the same property the union has, and the
        // same reason: a duplicate would be applied twice by the fold.
        await Seed(1, new SeatReservedEvent("a1", "s7"), SeatA1.ToString(), StudentS7.ToString());

        var events = await Context.GetEvents(TagQuery.AllOf(SeatA1, StudentS7));

        events.Should().ContainSingle();
    }

    [Fact]
    public async Task An_intersection_boundary_narrows_by_type_position_and_date_too()
    {
        var day = new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.Zero);
        var both = new[] { SeatA1.ToString(), StudentS7.ToString() };

        await Seed(1, new SeatReservedEvent("a1", "s7"), day, both);
        await Seed(2, new SeatReleasedEvent("a1"), day.AddDays(1), both);
        await Seed(3, new SeatReservedEvent("a1", "s7"), day.AddDays(2), SeatA1.ToString());

        var query = TagQuery.AllOf(SeatA1, StudentS7);

        (await Context.GetEvents(query, [typeof(SeatReleasedEvent)])).Should().ContainSingle();
        (await Context.GetEventsUpToPosition(query, 1)).Should().ContainSingle();
        (await Context.GetEventsFromDate(query, day.AddDays(1))).Should().ContainSingle();
    }

    [Fact]
    public async Task An_unregistered_event_type_is_reported_rather_than_skipped()
    {
        await Seed(1, new SeatReservedEvent("a1", "s7"), SeatA1.ToString());
        TypeBindings.EventTypeBindings = new Dictionary<string, Type>();

        var act = async () => await Context.GetEvents(TagQuery.AnyOf(SeatA1));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*SeatReserved:1*");
    }
}
