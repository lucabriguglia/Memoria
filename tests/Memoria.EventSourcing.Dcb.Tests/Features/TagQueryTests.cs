using FluentAssertions;
using Xunit;

namespace Memoria.EventSourcing.Dcb.Tests.Features;

public class TagQueryTests
{
    [Fact]
    public void A_query_holds_the_tags_it_was_built_from()
    {
        var query = TagQuery.AnyOf(new Tag("course", "c1"), new Tag("student", "s7"));

        query.Tags.Should().BeEquivalentTo([new Tag("course", "c1"), new Tag("student", "s7")]);
    }

    [Fact]
    public void A_query_matches_an_event_carrying_any_of_its_tags()
    {
        // 1.8.0 ships disjunction only. Conjunction is a deliberate omission, not an oversight.
        var query = TagQuery.AnyOf(new Tag("course", "c1"), new Tag("student", "s7"));

        query.Matches([new Tag("course", "c1")]).Should().BeTrue();
        query.Matches([new Tag("student", "s7"), new Tag("course", "c9")]).Should().BeTrue();
        query.Matches([new Tag("course", "c9")]).Should().BeFalse();
        query.Matches([]).Should().BeFalse();
    }

    [Fact]
    public void Duplicate_tags_collapse()
    {
        var query = TagQuery.AnyOf(new Tag("course", "c1"), new Tag("course", "c1"));

        query.Tags.Should().ContainSingle();
    }

    [Fact]
    public void A_query_needs_at_least_one_tag()
    {
        // A query over nothing would make an append condition vacuous, which reads as "append
        // unconditionally" — something the caller should say by passing no condition at all.
        var act = () => TagQuery.AnyOf();

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Queries_are_equal_by_tag_set_regardless_of_order()
    {
        // Slice 5 keys a snapshot on the query it was folded under, so two spellings of the same
        // boundary must not produce two snapshots.
        var one = TagQuery.AnyOf(new Tag("course", "c1"), new Tag("student", "s7"));
        var other = TagQuery.AnyOf(new Tag("student", "s7"), new Tag("course", "c1"));

        one.Should().Be(other);
        one.GetHashCode().Should().Be(other.GetHashCode());
    }

    [Fact]
    public void Different_queries_are_not_equal()
    {
        TagQuery.AnyOf(new Tag("course", "c1"))
            .Should().NotBe(TagQuery.AnyOf(new Tag("course", "c2")));

        TagQuery.AnyOf(new Tag("course", "c1"))
            .Should().NotBe(TagQuery.AnyOf(new Tag("course", "c1"), new Tag("student", "s7")));
    }

    [Fact]
    public void A_query_renders_to_a_stable_canonical_form()
    {
        // Order-independent and therefore usable as a persisted key.
        var one = TagQuery.AnyOf(new Tag("student", "s7"), new Tag("course", "c1"));
        var other = TagQuery.AnyOf(new Tag("course", "c1"), new Tag("student", "s7"));

        one.ToString().Should().Be(other.ToString());
        one.ToString().Should().Contain("course:c1").And.Contain("student:s7");
    }
}
