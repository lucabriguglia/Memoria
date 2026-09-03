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

    [Fact]
    public void An_intersection_query_holds_the_tags_it_was_built_from()
    {
        var query = TagQuery.AllOf(new Tag("course", "c1"), new Tag("student", "s7"));

        query.Tags.Should().BeEquivalentTo([new Tag("course", "c1"), new Tag("student", "s7")]);
    }

    [Fact]
    public void An_intersection_query_matches_only_an_event_carrying_every_one_of_its_tags()
    {
        var query = TagQuery.AllOf(new Tag("course", "c1"), new Tag("student", "s7"));

        query.Matches([new Tag("course", "c1"), new Tag("student", "s7")]).Should().BeTrue();
        query.Matches([new Tag("course", "c1")]).Should().BeFalse();
        query.Matches([new Tag("student", "s7")]).Should().BeFalse();
        query.Matches([]).Should().BeFalse();
    }

    [Fact]
    public void An_intersection_query_matches_an_event_carrying_more_than_it_asks_for()
    {
        // An event concerns whatever it concerns. A boundary asks that its tags are among them, not
        // that they are all of them.
        var query = TagQuery.AllOf(new Tag("course", "c1"), new Tag("student", "s7"));

        query.Matches([new Tag("course", "c1"), new Tag("student", "s7"), new Tag("term", "t3")])
            .Should().BeTrue();
    }

    [Fact]
    public void One_tag_is_one_boundary_however_it_is_spelled()
    {
        // Union and intersection of a single tag are the same set of events, so they must be the
        // same boundary — otherwise they would key two snapshots of one state.
        var union = TagQuery.AnyOf(new Tag("course", "c1"));
        var intersection = TagQuery.AllOf(new Tag("course", "c1"));

        intersection.Should().Be(union);
        intersection.GetHashCode().Should().Be(union.GetHashCode());
        intersection.ToString().Should().Be(union.ToString());
    }

    [Fact]
    public void A_union_and_an_intersection_over_the_same_tags_are_different_boundaries()
    {
        // They select different events, so a snapshot folded under one is not an answer for the
        // other. The canonical form has to say so, because it is what keys the snapshot.
        var union = TagQuery.AnyOf(new Tag("course", "c1"), new Tag("student", "s7"));
        var intersection = TagQuery.AllOf(new Tag("course", "c1"), new Tag("student", "s7"));

        intersection.Should().NotBe(union);
        intersection.ToString().Should().NotBe(union.ToString());
    }

    [Fact]
    public void Duplicate_tags_collapse_in_an_intersection_query()
    {
        var query = TagQuery.AllOf(new Tag("course", "c1"), new Tag("course", "c1"));

        query.Tags.Should().ContainSingle();
    }

    [Fact]
    public void An_intersection_query_needs_at_least_one_tag()
    {
        var act = () => TagQuery.AllOf();

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void An_intersection_query_is_equal_and_renders_regardless_of_tag_order()
    {
        var one = TagQuery.AllOf(new Tag("student", "s7"), new Tag("course", "c1"));
        var other = TagQuery.AllOf(new Tag("course", "c1"), new Tag("student", "s7"));

        one.Should().Be(other);
        one.GetHashCode().Should().Be(other.GetHashCode());
        one.ToString().Should().Be(other.ToString());
    }

    [Fact]
    public void Two_different_boundaries_never_render_to_the_same_canonical_form()
    {
        // A tag value may contain any character, including the separators the canonical form uses to
        // join tags. Without escaping, these two spell the same string — and since that string keys
        // the snapshot, one boundary would read the other's fold back as its own.
        var intersection = TagQuery.AllOf(new Tag("x", "a"), new Tag("y", "b"));
        var union = TagQuery.AnyOf(new Tag("x", "a&y:b"));

        intersection.ToString().Should().NotBe(union.ToString());

        var commas = TagQuery.AnyOf(new Tag("x", "a"), new Tag("y", "b"));
        var oneTagWithAComma = TagQuery.AnyOf(new Tag("x", "a,y:b"));

        commas.ToString().Should().NotBe(oneTagWithAComma.ToString());
    }

    [Fact]
    public void A_query_exposes_the_groups_its_tags_combine_in()
    {
        // A store has to translate the boundary, and the tags alone do not say how they combine.
        TagQuery.AnyOf(new Tag("course", "c1"), new Tag("student", "s7"))
            .TagGroups.Should().HaveCount(2).And.OnlyContain(group => group.Count == 1);

        TagQuery.AllOf(new Tag("course", "c1"), new Tag("student", "s7"))
            .TagGroups.Should().ContainSingle()
            .Which.Should().HaveCount(2);
    }
}
