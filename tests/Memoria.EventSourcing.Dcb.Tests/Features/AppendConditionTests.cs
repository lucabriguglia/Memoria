using FluentAssertions;
using Xunit;

namespace Memoria.EventSourcing.Dcb.Tests.Features;

public class AppendConditionTests
{
    [Fact]
    public void A_condition_names_a_boundary_and_the_position_it_was_read_at()
    {
        var query = TagQuery.AnyOf(new Tag("seat", "a1"));

        var condition = new AppendCondition(query, 42);

        condition.Query.Should().Be(query);
        condition.AfterPosition.Should().Be(42);
    }

    [Fact]
    public void A_condition_can_assert_that_a_boundary_has_never_been_written_to()
    {
        var condition = AppendCondition.NothingAppendedFor(TagQuery.AnyOf(new Tag("seat", "a1")));

        condition.AfterPosition.Should().Be(AppendCondition.NoEvents);
        AppendCondition.NoEvents.Should().Be(0, "positions start at 1, so 0 means the boundary is empty");
    }

    [Fact]
    public void A_position_cannot_be_negative()
    {
        var act = () => new AppendCondition(TagQuery.AnyOf(new Tag("seat", "a1")), -1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void A_condition_needs_a_query()
    {
        var act = () => new AppendCondition(null!, 1);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Conditions_are_equal_by_boundary_and_position()
    {
        var query = TagQuery.AnyOf(new Tag("seat", "a1"));

        new AppendCondition(query, 42).Should().Be(new AppendCondition(query, 42));
        new AppendCondition(query, 42).Should().NotBe(new AppendCondition(query, 43));
    }
}
