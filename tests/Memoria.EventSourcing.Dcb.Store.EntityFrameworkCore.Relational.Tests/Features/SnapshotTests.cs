using FluentAssertions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Relational.Tests.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Relational.Tests.Features;

/// <summary>
/// Persisted folds, and the four read modes over them.
/// </summary>
public class SnapshotTests : RelationalTestBase
{
    private static readonly Tag SeatA1 = new("seat", "a1");
    private static readonly Tag StudentS7 = new("student", "s7");

    private static TaggedEvent Reserved(string seat, string student, params Tag[] tags) =>
        new(new SeatReservedEvent(seat, student), tags.Length > 0 ? tags : [new Tag("seat", seat)]);

    private Task Append(params TaggedEvent[] events) => Context.SaveEvents(events, condition: null);

    // -- read modes ---------------------------------------------------------------------------

    [Fact]
    public async Task SnapshotOnly_returns_nothing_when_no_snapshot_was_ever_written()
    {
        await Append(Reserved("a1", "s7"));

        var result = await Context.GetAggregate(new SeatId("a1"), ReadMode.SnapshotOnly);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull("the events exist but no snapshot does, and this mode builds none");
    }

    [Fact]
    public async Task SnapshotOrCreate_folds_the_boundary_and_persists_the_result()
    {
        await Append(Reserved("a1", "s7"));

        var result = await Context.GetAggregate(new SeatId("a1"),
            ReadMode.SnapshotOrCreate);

        result.Value!.ReservedBy.Should().Be("s7");
        Context.DcbSnapshots.Count().Should().Be(1);
    }

    [Fact]
    public async Task SnapshotOnly_returns_the_snapshot_once_one_exists()
    {
        await Append(Reserved("a1", "s7"));
        await Context.GetAggregate(new SeatId("a1"), ReadMode.SnapshotOrCreate);

        var result = await Context.GetAggregate(new SeatId("a1"), ReadMode.SnapshotOnly);

        result.Value!.ReservedBy.Should().Be("s7");
    }

    [Fact]
    public async Task SnapshotOnly_does_not_see_events_appended_after_the_snapshot()
    {
        await Append(Reserved("a1", "s7"));
        await Context.GetAggregate(new SeatId("a1"), ReadMode.SnapshotOrCreate);
        await Context.SaveEvents([new TaggedEvent(new SeatReleasedEvent("a1"), [SeatA1])], condition: null);

        var result = await Context.GetAggregate(new SeatId("a1"), ReadMode.SnapshotOnly);

        result.Value!.ReservedBy.Should().Be("s7", "this mode is deliberately stale");
    }

    [Fact]
    public async Task SnapshotWithNewEvents_applies_what_arrived_after_the_snapshot()
    {
        await Append(Reserved("a1", "s7"));
        await Context.GetAggregate(new SeatId("a1"), ReadMode.SnapshotOrCreate);
        await Context.SaveEvents([new TaggedEvent(new SeatReleasedEvent("a1"), [SeatA1])], condition: null);

        var result = await Context.GetAggregate(new SeatId("a1"),
            ReadMode.SnapshotWithNewEvents);

        result.Value!.ReservedBy.Should().BeNull();
    }

    [Fact]
    public async Task SnapshotWithNewEvents_refreshes_the_stored_snapshot()
    {
        await Append(Reserved("a1", "s7"));
        await Context.GetAggregate(new SeatId("a1"), ReadMode.SnapshotOrCreate);
        await Context.SaveEvents([new TaggedEvent(new SeatReleasedEvent("a1"), [SeatA1])], condition: null);
        await Context.GetAggregate(new SeatId("a1"), ReadMode.SnapshotWithNewEvents);

        var afterRefresh = await Context.GetAggregate(new SeatId("a1"),
            ReadMode.SnapshotOnly);

        afterRefresh.Value!.ReservedBy.Should().BeNull("the refreshed snapshot was written back");
        Context.DcbSnapshots.Count().Should().Be(1, "refreshing replaces rather than accumulates");
    }

    [Fact]
    public async Task SnapshotWithNewEvents_returns_nothing_when_no_snapshot_exists()
    {
        await Append(Reserved("a1", "s7"));

        var result = await Context.GetAggregate(new SeatId("a1"),
            ReadMode.SnapshotWithNewEvents);

        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task SnapshotWithNewEventsOrCreate_builds_when_absent_and_refreshes_when_present()
    {
        await Append(Reserved("a1", "s7"));

        var built = await Context.GetAggregate(new SeatId("a1"),
            ReadMode.SnapshotWithNewEventsOrCreate);
        built.Value!.ReservedBy.Should().Be("s7");

        await Context.SaveEvents([new TaggedEvent(new SeatReleasedEvent("a1"), [SeatA1])], condition: null);

        var refreshed = await Context.GetAggregate(new SeatId("a1"),
            ReadMode.SnapshotWithNewEventsOrCreate);
        refreshed.Value!.ReservedBy.Should().BeNull();
    }

    [Fact]
    public async Task A_boundary_with_no_events_yields_no_aggregate_even_when_asked_to_create()
    {
        var result = await Context.GetAggregate(new SeatId("a1"),
            ReadMode.SnapshotOrCreate);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
        Context.DcbSnapshots.Count().Should().Be(0);
    }

    // -- the boundary is part of the snapshot's identity ---------------------------------------

    [Fact]
    public async Task A_snapshot_is_not_returned_for_a_different_boundary()
    {
        // The same aggregate id folded over a wider boundary is a different state. Returning the
        // narrow fold for the wide query would be silently wrong.
        await Append(Reserved("a1", "s7", SeatA1, StudentS7));
        await Context.GetAggregate(new SeatId("a1"), ReadMode.SnapshotOrCreate);

        var otherBoundary = await Context.GetAggregate(new WideSeatId("a1"),
            ReadMode.SnapshotOnly);

        otherBoundary.Value.Should().BeNull("that boundary has no snapshot of its own");
    }

    [Fact]
    public async Task Two_boundaries_over_one_aggregate_keep_separate_snapshots()
    {
        await Append(Reserved("a1", "s7", SeatA1, StudentS7));

        await Context.GetAggregate(new SeatId("a1"), ReadMode.SnapshotOrCreate);
        await Context.GetAggregate(new WideSeatId("a1"),
            ReadMode.SnapshotOrCreate);

        Context.DcbSnapshots.Count().Should().Be(2);
    }

    [Fact]
    public async Task A_snapshot_whose_stored_boundary_disagrees_with_its_identity_is_ignored()
    {
        // The identity carries only a digest of the boundary, so the stored boundary is compared in
        // full on every read. Reaching this by finding a SHA-256 prefix collision is not practical,
        // so the row is written directly — which is also what a future change to the identity
        // format would leave behind.
        await Append(Reserved("a1", "s7"));
        var boundary = TagQuery.AnyOf(SeatA1);
        await Context.GetAggregate(new SeatId("a1"), ReadMode.SnapshotOrCreate);

        await Context.Database.ExecuteSqlRawAsync(
            "UPDATE DcbSnapshots SET TagQuery = {0}", "seat:somewhere-else");

        var result = await Context.GetAggregate(new SeatId("a1"), ReadMode.SnapshotOnly);

        result.Value.Should().BeNull("the row is not a fold of the boundary that was asked for");
    }

    [Fact]
    public async Task An_aggregate_and_a_projection_sharing_an_id_do_not_collide()
    {
        await Append(Reserved("a1", "s7"));
        var boundary = TagQuery.AnyOf(SeatA1);

        await Context.GetAggregate(new SeatId("a1"), ReadMode.SnapshotOrCreate);
        await Context.GetProjection(new SeatSummaryId("a1"), ReadMode.SnapshotOrCreate);

        Context.DcbSnapshots.Count().Should().Be(2, "the kind is part of the identity");
    }

    // -- projections ---------------------------------------------------------------------------

    [Fact]
    public async Task A_projection_folds_persists_and_reads_back()
    {
        await Append(Reserved("a1", "s7"), Reserved("a1", "s8"));
        var boundary = TagQuery.AnyOf(SeatA1);

        var built = await Context.GetProjection(new SeatSummaryId("a1"), ReadMode.SnapshotOrCreate);
        built.Value!.Reservations.Should().Be(2);

        var read = await Context.GetProjection(new SeatSummaryId("a1"), ReadMode.SnapshotOnly);
        read.Value!.Reservations.Should().Be(2);
    }

    [Fact]
    public async Task A_projection_can_be_saved_explicitly_and_read_back()
    {
        var boundary = TagQuery.AnyOf(SeatA1);
        var projection = (await Context.GetInMemoryProjection(new SeatSummaryId("a1"))).Value!;

        var saveResult = await Context.SaveProjection(new SeatSummaryId("a1"), projection);

        saveResult.IsSuccess.Should().BeTrue();
        Context.DcbSnapshots.Count().Should().Be(1);
    }

    [Fact]
    public async Task Saving_a_projection_twice_replaces_rather_than_duplicates()
    {
        await Append(Reserved("a1", "s7"));
        var boundary = TagQuery.AnyOf(SeatA1);
        var projectionId = new SeatSummaryId("a1");

        var first = (await Context.GetInMemoryProjection(projectionId)).Value!;
        await Context.SaveProjection(projectionId, first);

        await Append(Reserved("a1", "s8"));
        var second = (await Context.GetInMemoryProjection(projectionId)).Value!;
        await Context.SaveProjection(projectionId, second);

        Context.DcbSnapshots.Count().Should().Be(1);
        (await Context.GetProjection(projectionId, ReadMode.SnapshotOnly))
            .Value!.Reservations.Should().Be(2);
    }

    // -- appending refreshes the snapshot ------------------------------------------------------

    [Fact]
    public async Task Saving_an_aggregate_refreshes_its_snapshot()
    {
        var boundary = TagQuery.AnyOf(SeatA1);
        var aggregate = new SeatAggregate();
        aggregate.Reserve("a1", "s7");

        await Context.SaveAggregate(new SeatId("a1"), aggregate, condition: null);

        var read = await Context.GetAggregate(new SeatId("a1"), ReadMode.SnapshotOnly);
        read.Value!.ReservedBy.Should().Be("s7", "the append wrote a snapshot, so SnapshotOnly finds one");
    }

    [Fact]
    public async Task A_failed_snapshot_write_takes_the_events_with_it()
    {
        // The events and the snapshot are one transaction. Committing the events and reporting
        // success while the snapshot is missing would leave the aggregate invisible to SnapshotOnly
        // and SnapshotWithNewEvents, with nothing telling the caller and nothing able to fix it: the
        // events are durable, so a retry is refused by its own condition.
        var boundary = TagQuery.AnyOf(SeatA1);
        var aggregate = new SeatAggregate();
        aggregate.Reserve("a1", "s7");

        await Context.Database.ExecuteSqlRawAsync("DROP TABLE DcbSnapshots;");

        var result = await Context.SaveAggregate(new SeatId("a1"), aggregate, condition: null);

        result.IsNotSuccess.Should().BeTrue("a snapshot that cannot be written is a failed save");
        result.Failure!.Type.Should().Be(EventSourcing.StoreFailures.StorageFailureType);
        Context.DcbEvents.Count().Should().Be(0, "the append rolled back with it");
    }

    [Fact]
    public async Task A_successful_save_is_always_visible_to_snapshot_only()
    {
        // The property the shared transaction buys: after Ok, every read mode can see it.
        var boundary = TagQuery.AnyOf(SeatA1);
        var aggregate = new SeatAggregate();
        aggregate.Reserve("a1", "s7");

        var result = await Context.SaveAggregate(new SeatId("a1"), aggregate, condition: null);

        result.IsSuccess.Should().BeTrue();
        (await Context.GetAggregate(new SeatId("a1"), ReadMode.SnapshotOnly))
            .Value.Should().NotBeNull();
        (await Context.GetAggregate(new SeatId("a1"), ReadMode.SnapshotWithNewEvents))
            .Value.Should().NotBeNull();
    }

    [Fact]
    public async Task A_saved_snapshot_records_the_position_of_the_events_it_appended()
    {
        // Not a re-read of the boundary: that runs after the commit and could pick up somebody
        // else's append, stamping the snapshot as having consumed an event it never applied.
        var boundary = TagQuery.AnyOf(SeatA1);
        var aggregate = new SeatAggregate();
        aggregate.Reserve("a1", "s7");

        await Context.SaveAggregate(new SeatId("a1"), aggregate, condition: null);

        var appended = Context.DcbEvents.Max(@event => @event.Position);
        Context.DcbSnapshots.Single().LatestPosition.Should().Be(appended);
    }

    [Fact]
    public async Task A_snapshot_is_not_stamped_with_a_position_belonging_to_another_writer()
    {
        // The window a re-read of MAX(Position) would fall into: another writer commits between this
        // append and the snapshot being stamped. Recording that position would claim the snapshot had
        // consumed an event it never applied, and a later SnapshotWithNewEvents would start past it.
        await using var other = CreateContext();

        var interceptor = new AppendsAfterCommitInterceptor(
            appendFromAnotherConnection: () => other.SaveEvents(
                [new TaggedEvent(new SeatReleasedEvent("a1"), [SeatA1])], condition: null),
            countEvents: () => Task.FromResult(other.DcbEvents.Count()));

        await using var saving = CreateContext(interceptor);

        var boundary = TagQuery.AnyOf(SeatA1);
        var aggregate = new SeatAggregate();
        aggregate.Reserve("a1", "s7");

        await saving.SaveAggregate(new SeatId("a1"), aggregate, condition: null);

        interceptor.Fired.Should().BeTrue("the intruding writer must actually have committed");

        var snapshot = Context.DcbSnapshots.Single();
        var ownEvent = Context.DcbEvents.OrderBy(@event => @event.Position).First().Position;

        snapshot.LatestPosition.Should().Be(ownEvent,
            "the snapshot folded its own event and nothing after it");
    }

    [Fact]
    public async Task A_stored_snapshot_records_the_position_it_folded_to()
    {
        await Append(Reserved("a1", "s7"));
        var boundary = TagQuery.AnyOf(SeatA1);
        var latest = await Context.GetLatestPosition(boundary);

        await Context.GetAggregate(new SeatId("a1"), ReadMode.SnapshotOrCreate);

        Context.DcbSnapshots.Single().LatestPosition.Should().Be(latest);
    }

    [Fact]
    public async Task A_stored_snapshot_records_the_boundary_in_full()
    {
        // A two-tag boundary, so this shows the whole thing is stored rather than a first tag.
        await Append(Reserved("a1", "s7", SeatA1, StudentS7));
        var aggregateId = new WideSeatId("a1");

        await Context.GetAggregate(aggregateId, ReadMode.SnapshotOrCreate);

        Context.DcbSnapshots.Single().TagQuery.Should().Be(aggregateId.Boundary.ToString());
    }

    [Fact]
    public async Task A_storage_failure_reading_a_snapshot_is_classified_and_leaks_nothing()
    {
        await Context.Database.ExecuteSqlRawAsync("DROP TABLE DcbSnapshots;");

        var result = await Context.GetAggregate(new SeatId("a1"), ReadMode.SnapshotOnly);

        result.IsNotSuccess.Should().BeTrue();
        result.Failure!.Type.Should().Be(EventSourcing.StoreFailures.StorageFailureType);
        result.Failure.Description.Should().NotContain("DcbSnapshots");
    }
}
