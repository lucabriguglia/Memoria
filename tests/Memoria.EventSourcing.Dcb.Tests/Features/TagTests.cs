using FluentAssertions;
using Xunit;

namespace Memoria.EventSourcing.Dcb.Tests.Features;

public class TagTests
{
    [Fact]
    public void A_tag_renders_as_key_colon_value()
    {
        new Tag("course", "c1").ToString().Should().Be("course:c1");
    }

    [Fact]
    public void A_tag_parses_from_its_rendered_form()
    {
        var tag = Tag.Parse("course:c1");

        tag.Key.Should().Be("course");
        tag.Value.Should().Be("c1");
    }

    [Fact]
    public void A_value_containing_a_colon_survives_the_round_trip()
    {
        // Only the first colon separates, so a value is free to contain more. This matters because
        // values are frequently identifiers the application did not choose, such as a URN.
        var tag = new Tag("resource", "urn:order:42");

        Tag.Parse(tag.ToString()).Should().Be(tag);
        Tag.Parse(tag.ToString()).Value.Should().Be("urn:order:42");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void A_tag_requires_a_key(string? key)
    {
        var act = () => new Tag(key!, "c1");

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void A_tag_requires_a_value(string? value)
    {
        var act = () => new Tag("course", value!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_key_may_not_contain_a_colon()
    {
        // Otherwise the rendered form is ambiguous and Parse cannot recover the original pair.
        var act = () => new Tag("cou:rse", "c1");

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("course")]
    [InlineData(":c1")]
    [InlineData("")]
    public void Parsing_rejects_a_string_that_is_not_a_rendered_tag(string candidate)
    {
        var act = () => Tag.Parse(candidate);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Tags_are_equal_by_key_and_value()
    {
        new Tag("course", "c1").Should().Be(new Tag("course", "c1"));
        new Tag("course", "c1").Should().NotBe(new Tag("course", "c2"));
        new Tag("course", "c1").Should().NotBe(new Tag("student", "c1"));
    }

    [Fact]
    public void Tag_equality_is_case_sensitive()
    {
        // Ordinal, matching how every other identifier in Memoria compares in .NET. A store whose
        // column collation is case-insensitive would disagree, so the store fixes the collation
        // rather than this type loosening.
        new Tag("course", "c1").Should().NotBe(new Tag("course", "C1"));
        new Tag("Course", "c1").Should().NotBe(new Tag("course", "c1"));
    }

    [Fact]
    public void Tags_can_be_used_as_dictionary_and_set_keys()
    {
        var set = new HashSet<Tag> { new("course", "c1"), new("course", "c1"), new("student", "s7") };

        set.Should().HaveCount(2);
    }
}
