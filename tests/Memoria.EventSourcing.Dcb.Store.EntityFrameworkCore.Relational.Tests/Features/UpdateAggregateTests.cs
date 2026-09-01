using FluentAssertions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Relational.Tests.Models;
using Xunit;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Relational.Tests.Features;

/// <summary>
/// Saving and updating aggregates over a boundary.
/// </summary>
public class UpdateAggregateTests : RelationalTestBase
{
    private static readonly Tag SeatA1 = new("seat", "a1");

    [Fact]
    public async Task Saving_an_aggregate_appends_its_staged_events()
    {
        var aggregate = new SeatAggregate();
        aggregate.Reserve("a1", "s7");

        var result = await Context.SaveAggregate(TagQuery.AnyOf(SeatA1), new SeatId("a1"), aggregate, condition: null);

        result.IsSuccess.Should().BeTrue();
        Context.DcbEvents.Count().Should().Be(1);
    }

    [Fact]
    public async Task Saving_an_aggregate_that_staged_nothing_succeeds_and_writes_nothing()
    {
        var result = await Context.SaveAggregate(TagQuery.AnyOf(SeatA1), new SeatId("a1"), new SeatAggregate(), condition: null);

        result.IsSuccess.Should().BeTrue();
        Context.DcbEvents.Count().Should().Be(0);
    }

    [Fact]
    public async Task Updating_an_aggregate_folds_decides_and_appends()
    {
        var boundary = TagQuery.AnyOf(SeatA1);

        var result = await Context.UpdateAggregate(boundary, new SeatId("a1"),
            aggregate => aggregate.Reserve("a1", "s7"));

        result.IsSuccess.Should().BeTrue();
        result.Value!.ReservedBy.Should().Be("s7");
        Context.DcbEvents.Count().Should().Be(1);
    }

    [Fact]
    public async Task Updating_an_aggregate_sees_what_was_already_appended()
    {
        var boundary = TagQuery.AnyOf(SeatA1);
        await Context.UpdateAggregate(boundary, new SeatId("a1"), aggregate => aggregate.Reserve("a1", "s7"));

        var result = await Context.UpdateAggregate(boundary, new SeatId("a1"),
            aggregate => aggregate.ReservedBy.Should().Be("s7", "the first reservation was folded in"));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Updating_an_aggregate_is_guarded_by_the_boundary_it_read()
    {
        var boundary = TagQuery.AnyOf(SeatA1);
        await using var other = CreateContext();

        // Interleave by hand: fold, then let another decision commit, then apply and append.
        var position = await Context.GetLatestPosition(boundary);
        var aggregate = (await Context.GetInMemoryAggregate(boundary, new SeatId("a1"))).Value!;

        await other.UpdateAggregate(boundary, new SeatId("a1"), other => other.Reserve("a1", "s8"));

        aggregate.Reserve("a1", "s7");
        var result = await Context.SaveAggregate(boundary, new SeatId("a1"), aggregate, new AppendCondition(boundary, position));

        result.IsNotSuccess.Should().BeTrue("the boundary moved between the fold and the append");
        result.Failure!.Type.Should().Be(EventSourcing.StoreFailures.ConcurrencyConflictType);
    }

    [Fact]
    public async Task An_update_is_refused_when_an_event_lands_between_its_two_reads()
    {
        // UpdateAggregate reads the boundary's position and then folds it. An event arriving between
        // those two reads must make the append fail: the decision did not see it. Reading the
        // position second instead would have it admit the event, and the append would be accepted on
        // a decision that never read it — losing the update, with the condition signing it off.
        await using var other = CreateContext();

        var interceptor = new AppendsBetweenReadsInterceptor(
            () => other.SaveEvents([new TaggedEvent(new SeatReservedEvent("a1", "s9"), [SeatA1])],
                condition: null));

        await using var updating = CreateContext(interceptor);

        var result = await updating.UpdateAggregate(TagQuery.AnyOf(SeatA1), new SeatId("a1"),
            aggregate => aggregate.Reserve("a1", "s7"));

        interceptor.Fired.Should().BeTrue("the intruding writer must actually have committed");
        result.IsNotSuccess.Should().BeTrue();
        result.Failure!.Type.Should().Be(EventSourcing.StoreFailures.ConcurrencyConflictType);
    }

    [Fact]
    public async Task An_update_refused_that_way_leaves_only_the_intruding_event()
    {
        await using var other = CreateContext();

        var interceptor = new AppendsBetweenReadsInterceptor(
            () => other.SaveEvents([new TaggedEvent(new SeatReservedEvent("a1", "s9"), [SeatA1])],
                condition: null));

        await using var updating = CreateContext(interceptor);

        await updating.UpdateAggregate(TagQuery.AnyOf(SeatA1), new SeatId("a1"),
            aggregate => aggregate.Reserve("a1", "s7"));

        var events = await Context.GetEvents(TagQuery.AnyOf(SeatA1));

        events.Should().ContainSingle("the refused update wrote nothing")
            .Which.Should().BeOfType<SeatReservedEvent>()
            .Which.StudentId.Should().Be("s9");
    }

    [Fact]
    public async Task An_update_that_stages_nothing_appends_nothing()
    {
        var result = await Context.UpdateAggregate(TagQuery.AnyOf(SeatA1), new SeatId("a1"), _ => { });

        result.IsSuccess.Should().BeTrue();
        Context.DcbEvents.Count().Should().Be(0);
    }
}
