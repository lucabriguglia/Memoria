using AwesomeAssertions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Tests.Models;
using Xunit;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Tests.Features;

public class GetEventHeadersTests : TestBase
{
    private static readonly Tag SeatA1 = new("seat", "a1");

    [Fact]
    public async Task A_header_carries_where_the_event_sits_what_it_was_stored_as_when_and_by_whom()
    {
        var appended = new DateTimeOffset(2024, 3, 1, 9, 30, 0, TimeSpan.Zero);
        await Seed(1, new SeatReservedEvent("a1", "s7"), appended, SeatA1.ToString());

        var headers = await Context.GetEventHeaders(TagQuery.AnyOf(SeatA1));

        // The audit interceptor names the row against the signed-in user the context was given.
        headers.Should().ContainSingle().Which.Should().Be(new DcbEventHeader(1, "SeatReserved:1", appended, "TestUser"));
    }

    [Fact]
    public async Task Headers_come_back_in_position_order_and_only_from_inside_the_boundary()
    {
        await Seed(3, new SeatReservedEvent("a1", "s9"), SeatA1.ToString());
        await Seed(1, new SeatReservedEvent("a1", "s7"), SeatA1.ToString());
        await Seed(2, new SeatReservedEvent("a2", "s8"), "seat:a2");

        var headers = await Context.GetEventHeaders(TagQuery.AnyOf(SeatA1));

        headers.Select(header => header.Position).Should().Equal(1, 3);
    }
}
