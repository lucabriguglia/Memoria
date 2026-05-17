using FluentAssertions;
using Xunit;

namespace Memoria.EventSourcing.Dcb.Tests;

public class TagSetTests
{
    [Fact]
    public void Equals_TwoSetsWithSameTagsInDifferentOrder_AreEqual()
    {
        var a = TagSet.Of(new Tag("showId", "S-1"), new Tag("seat", "A12"));
        var b = TagSet.Of(new Tag("seat", "A12"), new Tag("showId", "S-1"));

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Of_DuplicateKeys_Throws()
    {
        var act = () => TagSet.Of(new Tag("seat", "A12"), new Tag("seat", "A13"));

        act.Should().Throw<ArgumentException>().WithMessage("*duplicate*key*");
    }

    [Fact]
    public void Empty_IsEqualToItself()
    {
        TagSet.Empty.Should().Be(TagSet.Empty);
        TagSet.Empty.Tags.Should().BeEmpty();
    }

    [Fact]
    public void Contains_ReturnsTrue_WhenAllRequiredTagsArePresent()
    {
        var have = TagSet.Of(new Tag("showId", "S-1"), new Tag("seat", "A12"), new Tag("customerId", "C-9"));
        var required = TagSet.Of(new Tag("showId", "S-1"), new Tag("seat", "A12"));

        have.Contains(required).Should().BeTrue();
    }

    [Fact]
    public void Contains_ReturnsFalse_WhenAnyRequiredTagIsMissing()
    {
        var have = TagSet.Of(new Tag("showId", "S-1"));
        var required = TagSet.Of(new Tag("showId", "S-1"), new Tag("seat", "A12"));

        have.Contains(required).Should().BeFalse();
    }

    [Fact]
    public void Contains_ReturnsFalse_WhenValueDiffers()
    {
        var have = TagSet.Of(new Tag("seat", "A12"));
        var required = TagSet.Of(new Tag("seat", "A13"));

        have.Contains(required).Should().BeFalse();
    }
}
