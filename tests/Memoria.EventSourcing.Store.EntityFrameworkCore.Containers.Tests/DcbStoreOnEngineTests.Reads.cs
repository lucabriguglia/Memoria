using FluentAssertions;
using Memoria.EventSourcing.Dcb;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Relational.Tests.Models;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests.Fixtures;
using Xunit;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests;

/// <summary>
/// Reads and snapshots against the engines the store actually targets.
/// </summary>
/// <remarks>
/// The append path was proven on both engines first because it is the risky one. These close the
/// asymmetry that left: every read was proven only on SQLite and the in-memory provider, and each of
/// them translates to SQL that differs per provider — the boundary is a correlated <c>EXISTS</c>
/// over the tag collection, the latest position is a <c>MAX</c> over a nullable projection, the type
/// filter is a <c>Contains</c> over a list that may hold a null, and a snapshot is found by string
/// equality on a column whose collation this store pins deliberately.
/// </remarks>
public abstract partial class DcbStoreOnEngineTests
{
    [RequiresDockerFact]
    public Task ABoundaryReturnsOnlyTheEventsCarryingOneOfItsTags() =>
        WithDatabase(async (dbContext, _) =>
        {
            await dbContext.SaveEvents([Reserved("a1", "s7"), Reserved("a2", "s8")], condition: null);

            var events = await dbContext.GetEvents(TagQuery.AnyOf(SeatA1));

            events.Should().ContainSingle()
                .Which.Should().BeOfType<SeatReservedEvent>()
                .Which.SeatId.Should().Be("a1");
        });

    [RequiresDockerFact]
    public Task AnEventMatchingTwoTagsOfOneBoundaryIsReturnedOnce() =>
        WithDatabase(async (dbContext, _) =>
        {
            // A join would return it per matching tag row and the fold would apply it twice. The
            // query is written as a single EXISTS for this reason, and EXISTS is exactly the part
            // each provider translates for itself.
            await dbContext.SaveEvents([Reserved("a1", "s7", SeatA1, StudentS7)], condition: null);

            var events = await dbContext.GetEvents(TagQuery.AnyOf(SeatA1, StudentS7));

            events.Should().ContainSingle();
        });

    [RequiresDockerFact]
    public Task AnIntersectionBoundaryReturnsOnlyTheEventsCarryingEveryOneOfItsTags() =>
        WithDatabase(async (dbContext, _) =>
        {
            // A union is one EXISTS over an IN; an intersection is one EXISTS per tag, chained. That
            // is a different plan on every engine — each correlated subquery seeks the
            // (Tag, Position) primary key and the engine intersects them — so it is proven here and
            // not only on SQLite.
            await dbContext.SaveEvents(
                [
                    Reserved("a1", "s7", SeatA1, StudentS7),
                    Reserved("a1", "s9", SeatA1),
                    Reserved("a2", "s7", SeatA2, StudentS7)
                ],
                condition: null);

            var events = await dbContext.GetEvents(TagQuery.AllOf(SeatA1, StudentS7));

            events.Should().ContainSingle()
                .Which.Should().BeOfType<SeatReservedEvent>()
                .Which.StudentId.Should().Be("s7");
        });

    [RequiresDockerFact]
    public Task ReadsAreCaseSensitiveJustAsAppendsAre() =>
        WithDatabase(async (dbContext, _) =>
        {
            // The collation is pinned for the append condition's sake, but it decides reads too: on a
            // case-insensitive column this boundary would fold in an event it does not own.
            await dbContext.SaveEvents(
                [new TaggedEvent(new SeatReservedEvent("a1", "s7"), [new Tag("seat", "a1")])],
                condition: null);

            var events = await dbContext.GetEvents(TagQuery.AnyOf(new Tag("seat", "A1")));

            events.Should().BeEmpty("seat:A1 is a different tag from seat:a1");
        });

    [RequiresDockerFact]
    public Task ReadsCanBeBoundedByPositionAndDate() =>
        WithDatabase(async (dbContext, _) =>
        {
            await dbContext.SaveEvents([Reserved("a1", "s7")], condition: null);
            await dbContext.SaveEvents([Reserved("a1", "s8")], condition: null);
            await dbContext.SaveEvents([Reserved("a1", "s9")], condition: null);

            var boundary = TagQuery.AnyOf(SeatA1);
            var positions = dbContext.DcbEvents.OrderBy(@event => @event.Position)
                .Select(@event => @event.Position).ToList();

            (await dbContext.GetEventsFromPosition(boundary, positions[1])).Should().HaveCount(2);
            (await dbContext.GetEventsUpToPosition(boundary, positions[1])).Should().HaveCount(2);
            (await dbContext.GetEventsBetweenPositions(boundary, positions[1], positions[2]))
                .Should().HaveCount(2);

            var yesterday = DateTimeOffset.UtcNow.AddDays(-1);
            var tomorrow = DateTimeOffset.UtcNow.AddDays(1);

            (await dbContext.GetEventsFromDate(boundary, yesterday)).Should().HaveCount(3);
            (await dbContext.GetEventsUpToDate(boundary, tomorrow)).Should().HaveCount(3);
            (await dbContext.GetEventsBetweenDates(boundary, yesterday, tomorrow)).Should().HaveCount(3);
        });

    [RequiresDockerFact]
    public Task TheEventTypeFilterNarrowsWithinTheBoundary() =>
        WithDatabase(async (dbContext, _) =>
        {
            await dbContext.SaveEvents(
                [Reserved("a1", "s7"), new TaggedEvent(new SeatReleasedEvent("a1"), [SeatA1])],
                condition: null);

            var events = await dbContext.GetEvents(TagQuery.AnyOf(SeatA1), [typeof(SeatReleasedEvent)]);

            events.Should().ContainSingle().Which.Should().BeOfType<SeatReleasedEvent>();
        });

    [RequiresDockerFact]
    public Task AnEmptyBoundaryReportsTheNoEventsPosition() =>
        WithDatabase(async (dbContext, _) =>
        {
            // MAX over no rows: the query projects to a nullable so the provider returns NULL rather
            // than throwing, and the store maps that to a real position.
            var position = await dbContext.GetLatestPosition(TagQuery.AnyOf(SeatA1));

            position.Should().Be(AppendCondition.NoEvents);
        });

    [RequiresDockerFact]
    public Task FoldingABoundaryInMemoryRebuildsTheAggregate() =>
        WithDatabase(async (dbContext, _) =>
        {
            await dbContext.SaveEvents(
                [Reserved("a1", "s7"), new TaggedEvent(new SeatReleasedEvent("a1"), [SeatA1])],
                condition: null);

            var result = await dbContext.GetInMemoryAggregate(new SeatId("a1"));

            result.IsSuccess.Should().BeTrue();
            result.Value!.ReservedBy.Should().BeNull();
            result.Value.Version.Should().Be(2);
        });

    [RequiresDockerFact]
    public Task TheFourReadModesBehaveAsTheyDoOnSqlite() =>
        WithDatabase(async (dbContext, _) =>
        {
            var boundary = TagQuery.AnyOf(SeatA1);
            var aggregateId = new SeatId("a1");

            await dbContext.SaveEvents([Reserved("a1", "s7")], condition: null);

            (await dbContext.GetAggregate(aggregateId, ReadMode.SnapshotOnly))
                .Value.Should().BeNull("no snapshot exists yet and this mode builds none");

            (await dbContext.GetAggregate(aggregateId, ReadMode.SnapshotOrCreate))
                .Value!.ReservedBy.Should().Be("s7");

            await dbContext.SaveEvents(
                [new TaggedEvent(new SeatReleasedEvent("a1"), [SeatA1])], condition: null);

            (await dbContext.GetAggregate(aggregateId, ReadMode.SnapshotOnly))
                .Value!.ReservedBy.Should().Be("s7", "this mode is deliberately stale");

            (await dbContext.GetAggregate(aggregateId, ReadMode.SnapshotWithNewEvents))
                .Value!.ReservedBy.Should().BeNull();
        });

    [RequiresDockerFact]
    public Task ASnapshotIsNotReturnedForADifferentBoundary() =>
        WithDatabase(async (dbContext, _) =>
        {
            // The lookup compares the stored boundary as a string. That comparison runs under the
            // engine's collation like any other, so it is worth proving where it ships.
            await dbContext.SaveEvents([Reserved("a1", "s7", SeatA1, StudentS7)], condition: null);
            await dbContext.GetAggregate(new SeatId("a1"), ReadMode.SnapshotOrCreate);

            var wider = await dbContext.GetAggregate(new WideSeatId("a1"),
                ReadMode.SnapshotOnly);

            wider.Value.Should().BeNull("that boundary has no snapshot of its own");
            dbContext.DcbSnapshots.Count().Should().Be(1);
        });

    [RequiresDockerFact]
    public Task AProjectionFoldsPersistsAndReadsBack() =>
        WithDatabase(async (dbContext, _) =>
        {
            await dbContext.SaveEvents([Reserved("a1", "s7"), Reserved("a1", "s8")], condition: null);
            var boundary = TagQuery.AnyOf(SeatA1);

            (await dbContext.GetProjection(new SeatSummaryId("a1"), ReadMode.SnapshotOrCreate))
                .Value!.Reservations.Should().Be(2);

            (await dbContext.GetProjection(new SeatSummaryId("a1"), ReadMode.SnapshotOnly))
                .Value!.Reservations.Should().Be(2);
        });

    [RequiresDockerFact]
    public Task SavingASnapshotTwiceReplacesRatherThanDuplicates() =>
        WithDatabase(async (dbContext, _) =>
        {
            // The write is an existence probe followed by Add or Update. Both halves are provider
            // translated, and getting it wrong shows up as a duplicate key rather than a bad read.
            var boundary = TagQuery.AnyOf(SeatA1);
            var projectionId = new SeatSummaryId("a1");

            await dbContext.SaveEvents([Reserved("a1", "s7")], condition: null);
            var first = (await dbContext.GetInMemoryProjection(projectionId)).Value!;
            await dbContext.SaveProjection(projectionId, first);

            await dbContext.SaveEvents([Reserved("a1", "s8")], condition: null);
            var second = (await dbContext.GetInMemoryProjection(projectionId)).Value!;
            var result = await dbContext.SaveProjection(projectionId, second);

            result.IsSuccess.Should().BeTrue(
                result.Failure is null ? "the save should succeed" : result.Failure.Description);
            dbContext.DcbSnapshots.Count().Should().Be(1);
            (await dbContext.GetProjection(projectionId, ReadMode.SnapshotOnly))
                .Value!.Reservations.Should().Be(2);
        });
}
