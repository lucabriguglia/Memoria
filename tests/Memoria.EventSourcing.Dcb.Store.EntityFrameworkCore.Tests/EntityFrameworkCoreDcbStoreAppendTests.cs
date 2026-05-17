using FluentAssertions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Tests;

public class EntityFrameworkCoreDcbStoreAppendTests : TestBase
{
    [Fact]
    public async Task Append_PersistsEventsWithMonotonicGlobalPositionsStartingAtOne()
    {
        var append = await Store.Append(
        [
            EventToAppend.Of(new SeatReserved("A12"), new Tag("seat", "A12")),
            EventToAppend.Of(new SeatReserved("A13"), new Tag("seat", "A13"))
        ]);

        append.IsSuccess.Should().BeTrue();
        var stored = await DbContext.DcbEvents.AsNoTracking().OrderBy(e => e.GlobalPosition).ToListAsync();
        stored.Select(e => e.GlobalPosition).Should().Equal(1L, 2L);
        stored.Should().AllSatisfy(e => e.EventType.Should().Be("SeatReserved:1"));
    }

    [Fact]
    public async Task Append_PersistsTagsLinkedToEvents()
    {
        await Store.Append([EventToAppend.Of(new SeatReserved("A12"),
            new Tag("seat", "A12"),
            new Tag("showId", "S-1"))]);

        var tags = await DbContext.DcbEventTags.AsNoTracking().OrderBy(t => t.TagKey).ToListAsync();
        tags.Should().HaveCount(2);
        tags.Should().BeEquivalentTo(new[]
        {
            new { TagKey = "seat", TagValue = "A12", GlobalPosition = 1L },
            new { TagKey = "showId", TagValue = "S-1", GlobalPosition = 1L }
        });
    }

    [Fact]
    public async Task Append_AssignsTagsTheirEventsGlobalPosition()
    {
        await Store.Append([EventToAppend.Of(new SeatReserved("A12"), new Tag("seat", "A12"))]);
        await Store.Append([EventToAppend.Of(new SeatReleased("A12"), new Tag("seat", "A12"))]);

        var tags = await DbContext.DcbEventTags.AsNoTracking().OrderBy(t => t.GlobalPosition).ToListAsync();
        tags.Select(t => t.GlobalPosition).Should().Equal(1L, 2L);
    }

    [Fact]
    public async Task Append_ReturnsAppendedEventsInOrder()
    {
        var append = await Store.Append(
        [
            EventToAppend.Of(new SeatReserved("A12"), new Tag("seat", "A12")),
            EventToAppend.Of(new SeatReleased("A12"), new Tag("seat", "A12"))
        ]);

        append.Value!.Appended.Select(e => e.GlobalPosition).Should().Equal(1L, 2L);
        append.Value.LastPosition.Should().Be(2L);
    }

    [Fact]
    public async Task Append_EmptyEventsList_IsNoOp()
    {
        var append = await Store.Append([]);

        append.IsSuccess.Should().BeTrue();
        append.Value!.Appended.Should().BeEmpty();
        append.Value.LastPosition.Should().Be(0);
        (await DbContext.DcbEvents.CountAsync()).Should().Be(0);
    }
}
