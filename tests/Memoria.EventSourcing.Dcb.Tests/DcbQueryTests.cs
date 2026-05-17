using FluentAssertions;
using Xunit;

namespace Memoria.EventSourcing.Dcb.Tests;

public class DcbQueryTests
{
    private sealed record SeatReserved;
    private sealed record SeatReleased;
    private sealed record PaymentTaken;

    [Fact]
    public void Matches_TypeInListAndAllTagsPresent_ReturnsTrue()
    {
        var query = DcbQuery.Of(new QueryItem(
            types: [typeof(SeatReserved)],
            tags: TagSet.Of(new Tag("seat", "A12"))));

        var matches = query.Matches(typeof(SeatReserved), TagSet.Of(new Tag("seat", "A12"), new Tag("showId", "S-1")));

        matches.Should().BeTrue();
    }

    [Fact]
    public void Matches_TypeNotInList_ReturnsFalse()
    {
        var query = DcbQuery.Of(new QueryItem(
            types: [typeof(SeatReserved)],
            tags: TagSet.Empty));

        var matches = query.Matches(typeof(PaymentTaken), TagSet.Empty);

        matches.Should().BeFalse();
    }

    [Fact]
    public void Matches_RequiredTagMissing_ReturnsFalse()
    {
        var query = DcbQuery.Of(new QueryItem(
            types: [typeof(SeatReserved)],
            tags: TagSet.Of(new Tag("seat", "A12"))));

        var matches = query.Matches(typeof(SeatReserved), TagSet.Of(new Tag("seat", "A13")));

        matches.Should().BeFalse();
    }

    [Fact]
    public void Matches_EmptyTypesActsAsWildcard_ReturnsTrueOnAnyType()
    {
        var query = DcbQuery.Of(new QueryItem(
            types: [],
            tags: TagSet.Of(new Tag("seat", "A12"))));

        query.Matches(typeof(SeatReserved), TagSet.Of(new Tag("seat", "A12"))).Should().BeTrue();
        query.Matches(typeof(SeatReleased), TagSet.Of(new Tag("seat", "A12"))).Should().BeTrue();
    }

    [Fact]
    public void Matches_AnyQueryItemMatching_ReturnsTrue()
    {
        var query = DcbQuery.Of(
            new QueryItem([typeof(SeatReserved)], TagSet.Of(new Tag("seat", "A12"))),
            new QueryItem([typeof(PaymentTaken)], TagSet.Of(new Tag("orderId", "O-7"))));

        query.Matches(typeof(PaymentTaken), TagSet.Of(new Tag("orderId", "O-7"))).Should().BeTrue();
    }

    [Fact]
    public void Matches_NoQueryItems_ReturnsFalse()
    {
        var query = DcbQuery.Of();

        query.Matches(typeof(SeatReserved), TagSet.Empty).Should().BeFalse();
    }
}
