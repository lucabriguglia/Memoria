using FluentAssertions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Relational.Tests.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Relational.Tests.Features;

/// <summary>
/// Saving aggregates, and refreshing their snapshots.
/// </summary>
/// <remarks>
/// <c>UpdateAggregate</c> is the snapshot refresh, matching the streamed store: read the latest
/// snapshot, fold what arrived after it, write it back. It appends nothing. A decision that produces
/// events reads the boundary, folds it, and calls <c>SaveAggregate</c> with a condition — spelled out
/// in <c>Saving_an_aggregate_is_guarded_by_the_boundary_it_read</c> below.
/// </remarks>
public class UpdateAggregateTests : RelationalTestBase
{
    private static readonly Tag SeatA1 = new("seat", "a1");

    private Task Append(params TaggedEvent[] events) => Context.SaveEvents(events, condition: null);

    private static TaggedEvent Reserved(string student) =>
        new(new SeatReservedEvent("a1", student), [SeatA1]);

    // -- saving --------------------------------------------------------------------------------

    [Fact]
    public async Task Saving_an_aggregate_appends_its_staged_events()
    {
        var aggregate = new SeatAggregate();
        aggregate.Reserve("a1", "s7");

        var result = await Context.SaveAggregate(new SeatId("a1"), aggregate,
            condition: null);

        result.IsSuccess.Should().BeTrue();
        Context.DcbEvents.Count().Should().Be(1);
    }

    [Fact]
    public async Task Saving_an_aggregate_that_staged_nothing_succeeds_and_writes_nothing()
    {
        var result = await Context.SaveAggregate(new SeatId("a1"), new SeatAggregate(), condition: null);

        result.IsSuccess.Should().BeTrue();
        Context.DcbEvents.Count().Should().Be(0);
    }

    [Fact]
    public async Task Saving_an_aggregate_is_guarded_by_the_boundary_it_read()
    {
        // The read-decide-append cycle a decision performs, written out. The position is read before
        // the fold: an event arriving between the two then makes the append fail rather than being
        // counted as seen by a decision that never read it.
        var boundary = TagQuery.AnyOf(SeatA1);
        await using var other = CreateContext();

        var position = await Context.GetLatestPosition(boundary);
        var aggregate = (await Context.GetInMemoryAggregate(new SeatId("a1"))).Value!;

        await other.SaveEvents([Reserved("s8")], condition: null);

        aggregate.Reserve("a1", "s7");
        var result = await Context.SaveAggregate(new SeatId("a1"), aggregate,
            new AppendCondition(boundary, position));

        result.IsNotSuccess.Should().BeTrue("the boundary moved between the fold and the append");
        result.Failure!.Type.Should().Be(EventSourcing.StoreFailures.ConcurrencyConflictType);
    }

    // -- refreshing ----------------------------------------------------------------------------

    [Fact]
    public async Task Updating_folds_the_boundary_and_writes_a_snapshot_when_there_is_none()
    {
        await Append(Reserved("s7"));

        var result = await Context.UpdateAggregate(new SeatId("a1"));

        result.IsSuccess.Should().BeTrue();
        result.Value!.ReservedBy.Should().Be("s7");
        Context.DcbSnapshots.Count().Should().Be(1);
    }

    [Fact]
    public async Task Updating_applies_only_what_arrived_after_the_snapshot()
    {
        var boundary = TagQuery.AnyOf(SeatA1);
        await Append(Reserved("s7"));
        await Context.UpdateAggregate(new SeatId("a1"));

        await Append(new TaggedEvent(new SeatReleasedEvent("a1"), [SeatA1]));

        var result = await Context.UpdateAggregate(new SeatId("a1"));

        result.Value!.ReservedBy.Should().BeNull();
        Context.DcbSnapshots.Count().Should().Be(1, "refreshing replaces rather than accumulates");
    }

    [Fact]
    public async Task Updating_makes_the_refreshed_state_visible_to_a_snapshot_only_read()
    {
        var boundary = TagQuery.AnyOf(SeatA1);
        await Append(Reserved("s7"));
        await Context.UpdateAggregate(new SeatId("a1"));
        await Append(new TaggedEvent(new SeatReleasedEvent("a1"), [SeatA1]));

        await Context.UpdateAggregate(new SeatId("a1"));

        (await Context.GetAggregate(new SeatId("a1"), ReadMode.SnapshotOnly))
            .Value!.ReservedBy.Should().BeNull("the refresh was written back");
    }

    [Fact]
    public async Task Updating_an_empty_boundary_yields_nothing()
    {
        var result = await Context.UpdateAggregate(new SeatId("a1"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull("there is no snapshot and no event to build one from");
        Context.DcbSnapshots.Count().Should().Be(0);
    }

    [Fact]
    public async Task Updating_appends_nothing()
    {
        // The distinction from the streamed store's UpdateAggregate that matters most: this is a
        // read path that happens to write a cache, not a way to record a decision.
        await Append(Reserved("s7"));

        await Context.UpdateAggregate(new SeatId("a1"));

        Context.DcbEvents.Count().Should().Be(1);
    }

    [Fact]
    public async Task Updating_with_nothing_new_leaves_the_snapshot_alone()
    {
        var boundary = TagQuery.AnyOf(SeatA1);
        await Append(Reserved("s7"));
        await Context.UpdateAggregate(new SeatId("a1"));

        var before = Context.DcbSnapshots.Single().LatestPosition;

        var result = await Context.UpdateAggregate(new SeatId("a1"));

        result.Value!.ReservedBy.Should().Be("s7");
        Context.DcbSnapshots.Single().LatestPosition.Should().Be(before);
    }

    // -- projections refresh exactly as aggregates do ------------------------------------------

    [Fact]
    public async Task Updating_a_projection_folds_the_boundary_and_writes_a_snapshot_when_there_is_none()
    {
        await Append(Reserved("s7"));

        var result = await Context.UpdateProjection(new SeatSummaryId("a1"));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Reservations.Should().Be(1);
        Context.DcbSnapshots.Count().Should().Be(1);
    }

    [Fact]
    public async Task Updating_a_projection_applies_only_what_arrived_after_the_snapshot()
    {
        var projectionId = new SeatSummaryId("a1");
        await Append(Reserved("s7"));
        await Context.UpdateProjection(projectionId);

        await Append(Reserved("s8"));

        var result = await Context.UpdateProjection(projectionId);

        result.Value!.Reservations.Should().Be(2);
        Context.DcbSnapshots.Count().Should().Be(1, "refreshing replaces rather than accumulates");
    }

    [Fact]
    public async Task Updating_an_empty_boundary_yields_no_projection()
    {
        var result = await Context.UpdateProjection(new SeatSummaryId("a1"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull("there is no snapshot and no event to build one from");
        Context.DcbSnapshots.Count().Should().Be(0);
    }

    [Fact]
    public async Task Updating_a_projection_appends_nothing()
    {
        await Append(Reserved("s7"));

        await Context.UpdateProjection(new SeatSummaryId("a1"));

        Context.DcbEvents.Count().Should().Be(1);
    }

    [Fact]
    public async Task A_projection_knows_the_boundary_it_was_folded_from()
    {
        // Tags live on the shared base, so a read model records what built it exactly as a write
        // model does — it simply never uses them to stage anything.
        await Append(Reserved("s7"));

        var result = await Context.UpdateProjection(new SeatSummaryId("a1"));

        result.Value!.Tags.Should().BeEquivalentTo([SeatA1]);
    }

    [Fact]
    public async Task A_storage_failure_while_updating_is_classified()
    {
        await Context.Database.ExecuteSqlRawAsync("DROP TABLE DcbSnapshots;");

        var result = await Context.UpdateAggregate(new SeatId("a1"));

        result.IsNotSuccess.Should().BeTrue();
        result.Failure!.Type.Should().Be(EventSourcing.StoreFailures.StorageFailureType);
        result.Failure.Description.Should().NotContain("DcbSnapshots");
    }
}
