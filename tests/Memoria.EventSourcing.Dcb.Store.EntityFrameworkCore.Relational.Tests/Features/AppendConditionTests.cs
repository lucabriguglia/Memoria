using FluentAssertions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Relational.Tests.Models;
using Memoria.EventSourcing.Domain;
using Microsoft.EntityFrameworkCore;
using Memoria.Results;
using Xunit;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Relational.Tests.Features;

/// <summary>
/// The append condition: the mechanism that makes a consistency boundary mean something.
/// </summary>
public class AppendConditionTests : RelationalTestBase
{
    private static readonly Tag SeatA1 = new("seat", "a1");
    private static readonly Tag SeatA2 = new("seat", "a2");
    private static readonly Tag StudentS7 = new("student", "s7");

    private static TaggedEvent Reserved(string seat, string student, params Tag[] tags) =>
        new(new SeatReservedEvent(seat, student), tags.Length > 0 ? tags : [new Tag("seat", seat)]);

    [Fact]
    public async Task An_unconditional_append_succeeds_and_positions_increase()
    {
        (await Context.SaveEvents([Reserved("a1", "s7")], condition: null)).IsSuccess.Should().BeTrue();
        (await Context.SaveEvents([Reserved("a1", "s8")], condition: null)).IsSuccess.Should().BeTrue();

        var positions = Context.DcbEvents.OrderBy(@event => @event.Position)
            .Select(@event => @event.Position).ToList();

        positions.Should().HaveCount(2);
        positions[1].Should().BeGreaterThan(positions[0]);
    }

    [Fact]
    public async Task An_append_conditioned_on_an_empty_boundary_succeeds()
    {
        var condition = AppendCondition.NothingAppendedFor(TagQuery.AnyOf(SeatA1));

        var result = await Context.SaveEvents([Reserved("a1", "s7")], condition);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task An_append_conditioned_on_the_current_position_succeeds()
    {
        await Context.SaveEvents([Reserved("a1", "s7")], condition: null);
        var position = await Context.GetLatestPosition(TagQuery.AnyOf(SeatA1));

        var result = await Context.SaveEvents([Reserved("a1", "s8")],
            new AppendCondition(TagQuery.AnyOf(SeatA1), position));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task An_append_conditioned_on_a_stale_position_is_refused()
    {
        await Context.SaveEvents([Reserved("a1", "s7")], condition: null);
        var stale = AppendCondition.NothingAppendedFor(TagQuery.AnyOf(SeatA1));

        var result = await Context.SaveEvents([Reserved("a1", "s8")], stale);

        result.IsNotSuccess.Should().BeTrue();
        result.Failure!.Type.Should().Be(EventSourcing.StoreFailures.ConcurrencyConflictType);
        result.Failure.ErrorCode.Should().Be(ErrorCode.Conflict);
    }

    [Fact]
    public async Task A_refused_append_writes_nothing()
    {
        await Context.SaveEvents([Reserved("a1", "s7")], condition: null);
        var stale = AppendCondition.NothingAppendedFor(TagQuery.AnyOf(SeatA1));

        await Context.SaveEvents([Reserved("a1", "s8")], stale);

        Context.DcbEvents.Count().Should().Be(1, "the refused append must leave no trace");
    }

    [Fact]
    public async Task A_conflict_carries_the_actual_position_so_a_retry_needs_no_extra_read()
    {
        await Context.SaveEvents([Reserved("a1", "s7")], condition: null);
        var actual = await Context.GetLatestPosition(TagQuery.AnyOf(SeatA1));

        var result = await Context.SaveEvents([Reserved("a1", "s8")],
            AppendCondition.NothingAppendedFor(TagQuery.AnyOf(SeatA1)));

        result.Failure!.Tags.Should().Contain("expectedPosition", "0");
        result.Failure.Tags.Should().Contain("latestPosition", actual.ToString());
    }

    [Fact]
    public async Task Two_appends_on_disjoint_boundaries_both_succeed()
    {
        // The test that proves this is a dynamic boundary and not one global lock. Both decisions
        // read their own boundary, then both append, interleaved.
        await using var other = CreateContext();

        var first = TagQuery.AnyOf(SeatA1);
        var second = TagQuery.AnyOf(SeatA2);

        var firstPosition = await Context.GetLatestPosition(first);
        var secondPosition = await other.GetLatestPosition(second);

        var firstResult = await Context.SaveEvents([Reserved("a1", "s7")], new AppendCondition(first, firstPosition));
        var secondResult = await other.SaveEvents([Reserved("a2", "s8")], new AppendCondition(second, secondPosition));

        firstResult.IsSuccess.Should().BeTrue();
        secondResult.IsSuccess.Should().BeTrue("a boundary over seat:a2 is untouched by an append to seat:a1");
    }

    [Fact]
    public async Task Two_appends_on_overlapping_boundaries_cannot_both_succeed()
    {
        await using var other = CreateContext();

        var boundary = TagQuery.AnyOf(SeatA1);

        // Both read the same boundary at the same position, then both try to append.
        var firstPosition = await Context.GetLatestPosition(boundary);
        var secondPosition = await other.GetLatestPosition(boundary);
        firstPosition.Should().Be(secondPosition);

        var firstResult = await Context.SaveEvents([Reserved("a1", "s7")], new AppendCondition(boundary, firstPosition));
        var secondResult = await other.SaveEvents([Reserved("a1", "s8")], new AppendCondition(boundary, secondPosition));

        firstResult.IsSuccess.Should().BeTrue();
        secondResult.IsNotSuccess.Should().BeTrue();
        secondResult.Failure!.Type.Should().Be(EventSourcing.StoreFailures.ConcurrencyConflictType);

        Context.DcbEvents.Count().Should().Be(1);
    }

    [Fact]
    public async Task Boundaries_overlapping_on_only_one_tag_still_contend()
    {
        // The decisions are about different seats but the same student, so they overlap and one
        // must lose. This is the case a stream-per-seat model could not express at all.
        await using var other = CreateContext();

        var first = TagQuery.AnyOf(SeatA1, StudentS7);
        var second = TagQuery.AnyOf(SeatA2, StudentS7);

        var firstPosition = await Context.GetLatestPosition(first);
        var secondPosition = await other.GetLatestPosition(second);

        var firstResult = await Context.SaveEvents(
            [Reserved("a1", "s7", SeatA1, StudentS7)], new AppendCondition(first, firstPosition));
        var secondResult = await other.SaveEvents(
            [Reserved("a2", "s7", SeatA2, StudentS7)], new AppendCondition(second, secondPosition));

        firstResult.IsSuccess.Should().BeTrue();
        secondResult.IsNotSuccess.Should().BeTrue("both boundaries name student:s7");
    }

    [Fact]
    public async Task A_boundary_over_a_tag_with_no_events_is_still_defended()
    {
        // Nothing has ever been written under seat:a1, so there is no event for a MAX read to find.
        // The first writer under that tag must still invalidate a condition that named it.
        await using var other = CreateContext();

        var boundary = TagQuery.AnyOf(SeatA1);
        var condition = AppendCondition.NothingAppendedFor(boundary);
        var otherCondition = AppendCondition.NothingAppendedFor(boundary);

        var firstResult = await Context.SaveEvents([Reserved("a1", "s7")], condition);
        var secondResult = await other.SaveEvents([Reserved("a1", "s8")], otherCondition);

        firstResult.IsSuccess.Should().BeTrue();
        secondResult.IsNotSuccess.Should().BeTrue("only one decision may be the first under this tag");
    }

    [Fact]
    public async Task An_unconditional_append_still_invalidates_a_conditioned_one()
    {
        await using var other = CreateContext();

        var boundary = TagQuery.AnyOf(SeatA1);
        var position = await other.GetLatestPosition(boundary);

        await Context.SaveEvents([Reserved("a1", "s7")], condition: null);

        var conditioned = await other.SaveEvents([Reserved("a1", "s8")], new AppendCondition(boundary, position));

        conditioned.IsNotSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Two_unconditional_appends_over_the_same_tag_both_succeed()
    {
        // Neither read anything, so neither has anything to be invalidated. Failing one of them
        // would be a conflict nobody asked for.
        await using var other = CreateContext();

        var firstResult = await Context.SaveEvents([Reserved("a1", "s7")], condition: null);
        var secondResult = await other.SaveEvents([Reserved("a1", "s8")], condition: null);

        firstResult.IsSuccess.Should().BeTrue();
        secondResult.IsSuccess.Should().BeTrue();
        Context.DcbEvents.Count().Should().Be(2);
    }

    [Fact]
    public async Task An_append_of_nothing_succeeds_and_writes_nothing()
    {
        var result = await Context.SaveEvents([], AppendCondition.NothingAppendedFor(TagQuery.AnyOf(SeatA1)));

        result.IsSuccess.Should().BeTrue("a condition guards events, and there are none");
        Context.DcbEvents.Count().Should().Be(0);
    }

    [Fact]
    public async Task An_oversized_append_is_refused_before_anything_is_written()
    {
        var events = Enumerable.Range(0, 5).Select(index => Reserved("a1", $"s{index}")).ToArray();

        var result = await Context.SaveEvents(events, condition: null, maxEventsPerAppend: 4);

        result.IsNotSuccess.Should().BeTrue();
        result.Failure!.Type.Should().Be(EventSourcing.StoreFailures.BatchLimitExceededType);
        result.Failure.ErrorCode.Should().Be(ErrorCode.BadRequest);
        result.Failure.Tags.Should().Contain("requestedEventCount", "5").And.Contain("maximumEventCount", "4");
        Context.DcbEvents.Count().Should().Be(0);
    }

    [Fact]
    public async Task A_storage_failure_names_the_operation_and_leaks_no_provider_detail()
    {
        // The tag column is 255 characters; a longer one is rejected by the database itself.
        await Context.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER fail_append BEFORE INSERT ON DcbEvents BEGIN SELECT RAISE(ABORT, 'constraint FK_x on table DcbEventTags'); END;");

        var result = await Context.SaveEvents([Reserved("a1", "s7")], condition: null);

        result.IsNotSuccess.Should().BeTrue();
        result.Failure!.Type.Should().Be(EventSourcing.StoreFailures.StorageFailureType);
        result.Failure.Tags.Should().Contain("operation", "Append Events");
        result.Failure.Description.Should().NotContain("DcbEventTags").And.NotContain("FK_x");
    }

    [Fact]
    public async Task An_appended_event_is_readable_through_its_boundary()
    {
        await Context.SaveEvents([Reserved("a1", "s7", SeatA1, StudentS7)], condition: null);

        var bySeat = await Context.GetEvents(TagQuery.AnyOf(SeatA1));
        var byStudent = await Context.GetEvents(TagQuery.AnyOf(StudentS7));

        bySeat.Should().ContainSingle().Which.Should().BeOfType<SeatReservedEvent>();
        byStudent.Should().ContainSingle("the event carries both tags");
    }

    [Fact]
    public async Task An_appended_event_is_stamped_by_the_audit_interceptor()
    {
        TimeProvider.SetUtcNow(new DateTimeOffset(2026, 8, 31, 9, 0, 0, TimeSpan.Zero));

        await Context.SaveEvents([Reserved("a1", "s7")], condition: null);

        var stored = Context.DcbEvents.Single();
        stored.CreatedDate.Should().Be(new DateTimeOffset(2026, 8, 31, 9, 0, 0, TimeSpan.Zero));
        stored.CreatedBy.Should().Be("TestUser");
    }
}
