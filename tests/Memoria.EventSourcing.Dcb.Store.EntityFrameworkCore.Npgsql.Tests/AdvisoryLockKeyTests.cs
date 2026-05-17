using FluentAssertions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Npgsql;
using Xunit;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Npgsql.Tests;

public class AdvisoryLockKeyTests
{
    private static AppendCondition Condition(params Tag[] tags) =>
        new(DcbQuery.Of(new QueryItem([], TagSet.Of(tags))), AfterPosition: 0L);

    [Fact]
    public void Returns_one_distinct_key_per_unique_tag()
    {
        var keys = AdvisoryLockKey.ForCondition(Condition(
            new Tag("seat", "A12"),
            new Tag("showId", "S-1")));

        keys.Should().HaveCount(2);
        keys.Distinct().Should().HaveCount(2);
    }

    [Fact]
    public void Returns_keys_in_ascending_order()
    {
        var keys = AdvisoryLockKey.ForCondition(Condition(
            new Tag("seat", "A12"),
            new Tag("showId", "S-1"),
            new Tag("customerId", "C-9")));

        keys.Should().BeInAscendingOrder();
    }

    [Fact]
    public void Different_writers_with_overlapping_tags_pick_same_key_for_shared_tag()
    {
        var writerA = AdvisoryLockKey.ForCondition(Condition(
            new Tag("seat", "A12"), new Tag("showId", "S-1")));
        var writerB = AdvisoryLockKey.ForCondition(Condition(
            new Tag("seat", "A12"), new Tag("customerId", "C-9")));

        writerA.Intersect(writerB).Should().ContainSingle("the shared (seat, A12) tag must collapse to the same lock key");
    }

    [Fact]
    public void Different_values_for_same_key_produce_different_lock_keys()
    {
        var a12 = AdvisoryLockKey.ForCondition(Condition(new Tag("seat", "A12"))).Single();
        var a13 = AdvisoryLockKey.ForCondition(Condition(new Tag("seat", "A13"))).Single();

        a12.Should().NotBe(a13);
    }

    [Fact]
    public void Empty_condition_produces_no_lock_keys()
    {
        var keys = AdvisoryLockKey.ForCondition(new AppendCondition(DcbQuery.Of(), 0L));

        keys.Should().BeEmpty();
    }

    [Fact]
    public void Hash_is_deterministic_across_calls()
    {
        var a = AdvisoryLockKey.ForCondition(Condition(new Tag("seat", "A12"))).Single();
        var b = AdvisoryLockKey.ForCondition(Condition(new Tag("seat", "A12"))).Single();

        a.Should().Be(b);
    }

    [Fact]
    public void Deduplicates_tag_repeated_across_query_items()
    {
        var condition = new AppendCondition(
            DcbQuery.Of(
                new QueryItem([], TagSet.Of(new Tag("seat", "A12"))),
                new QueryItem([], TagSet.Of(new Tag("seat", "A12"), new Tag("showId", "S-1")))),
            AfterPosition: 0L);

        var keys = AdvisoryLockKey.ForCondition(condition);

        keys.Should().HaveCount(2);
    }
}
