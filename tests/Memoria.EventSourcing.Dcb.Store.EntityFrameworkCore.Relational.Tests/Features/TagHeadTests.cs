using FluentAssertions;
using FluentAssertions.Execution;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Relational.Tests.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Relational.Tests.Features;

/// <summary>
/// The tag head rows — the half of the append condition that survives a genuine interleaving.
/// </summary>
/// <remarks>
/// The <c>MAX(Position)</c> pre-check catches every conflict two <em>sequential</em> appends can
/// produce, so it alone makes the conflict tests pass. These cover what it cannot: the window
/// between one append reading a boundary and writing it, during which an overlapping append commits.
/// </remarks>
public class TagHeadTests : RelationalTestBase
{
    private static readonly Tag SeatA1 = new("seat", "a1");
    private static readonly Tag SeatA2 = new("seat", "a2");
    private static readonly Tag StudentS7 = new("student", "s7");

    private static TaggedEvent Reserved(string seat, string student, params Tag[] tags) =>
        new(new SeatReservedEvent(seat, student), tags.Length > 0 ? tags : [new Tag("seat", seat)]);

    private async Task<Guid?> TokenFor(Tag tag) =>
        (await Context.DcbTagHeads.AsNoTracking().SingleOrDefaultAsync(head => head.Tag == tag.ToString()))?.Token;

    [Fact]
    public void The_token_is_declared_as_a_concurrency_token()
    {
        // Unlike collation, this survives into the runtime model.
        var token = Context.Model
            .FindEntityType(typeof(DcbTagHeadEntity))!
            .FindProperty(nameof(DcbTagHeadEntity.Token))!;

        token.IsConcurrencyToken.Should().BeTrue(
            "without it the update carries no WHERE clause on the old value and cannot detect anything");
    }

    [Fact]
    public async Task An_append_creates_a_head_row_for_every_tag_it_writes_under()
    {
        await Context.SaveEvents([Reserved("a1", "s7", SeatA1, StudentS7)], condition: null);

        (await TokenFor(SeatA1)).Should().NotBeNull();
        (await TokenFor(StudentS7)).Should().NotBeNull();
    }

    [Fact]
    public async Task An_append_replaces_the_token_of_every_tag_it_writes_under()
    {
        await Context.SaveEvents([Reserved("a1", "s7")], condition: null);
        var before = await TokenFor(SeatA1);

        await Context.SaveEvents([Reserved("a1", "s8")], condition: null);

        (await TokenFor(SeatA1)).Should().NotBe(before!.Value);
    }

    [Fact]
    public async Task An_append_replaces_the_token_of_tags_its_condition_names_but_it_does_not_write()
    {
        // The subtle half of the algorithm. A decision that read student:s7 and then wrote only
        // under seat:a1 must still make a concurrent decision over student:s7 fail — otherwise two
        // decisions could each read the same student and both commit.
        await Context.SaveEvents([Reserved("a1", "s7", SeatA1)],
            AppendCondition.NothingAppendedFor(TagQuery.AnyOf(StudentS7)));

        var afterFirst = await TokenFor(StudentS7);
        afterFirst.Should().NotBeNull("the condition's tag is locked even though nothing was written under it");

        await Context.SaveEvents([Reserved("a2", "s9", SeatA2)],
            new AppendCondition(TagQuery.AnyOf(StudentS7), 0));

        (await TokenFor(StudentS7)).Should().NotBe(afterFirst!.Value);
    }

    [Fact]
    public async Task An_append_leaves_unrelated_tags_alone()
    {
        // If it did not, every boundary would contend with every other and this would be a global
        // lock wearing a tag-shaped hat.
        await Context.SaveEvents([Reserved("a2", "s9", SeatA2)], condition: null);
        var untouched = await TokenFor(SeatA2);

        await Context.SaveEvents([Reserved("a1", "s7", SeatA1)], condition: null);

        (await TokenFor(SeatA2)).Should().Be(untouched!.Value);
    }

    [Fact]
    public async Task A_conditioned_append_is_refused_when_its_tag_head_moves_mid_append()
    {
        // The interleaving the pre-check cannot see: the boundary is still at the expected position
        // when the append reads it, and an overlapping append commits before it writes.
        var interceptor = StaleTagHeadInterceptor.OnSameConnection(SeatA1.ToString());
        await using var racing = CreateContext(interceptor);

        var result = await racing.SaveEvents([Reserved("a1", "s7")],
            AppendCondition.NothingAppendedFor(TagQuery.AnyOf(SeatA1)));

        interceptor.Fired.Should().BeTrue("the race must actually have been simulated");
        result.IsNotSuccess.Should().BeTrue();
        result.Failure!.Type.Should().Be(EventSourcing.StoreFailures.ConcurrencyConflictType);
    }

    [Fact]
    public async Task An_append_refused_by_a_moving_tag_head_writes_nothing()
    {
        var interceptor = StaleTagHeadInterceptor.OnSameConnection(SeatA1.ToString());
        await using var racing = CreateContext(interceptor);

        await racing.SaveEvents([Reserved("a1", "s7")],
            AppendCondition.NothingAppendedFor(TagQuery.AnyOf(SeatA1)));

        Context.DcbEvents.Count().Should().Be(0, "the whole append rolls back, events included");
    }

    [Fact]
    public async Task An_unconditional_append_is_not_refused_when_its_tag_head_moves_mid_append()
    {
        // It read nothing, so there is nothing for a moved head to invalidate. Failing here would be
        // a conflict the caller never asked to be protected from.
        var interceptor = StaleTagHeadInterceptor.OnSameConnection(SeatA1.ToString());
        await using var racing = CreateContext(interceptor);

        var result = await racing.SaveEvents([Reserved("a1", "s7")], condition: null);

        interceptor.Fired.Should().BeTrue();
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task An_intersection_condition_claims_a_head_row_for_every_tag_it_names()
    {
        // An intersection names both tags and is moved only by an event carrying both — so watching
        // either one alone would be enough. It claims both anyway: conservative, and the same rule
        // the union follows, so the append path needs no second notion of which tags matter.
        await Context.SaveEvents([Reserved("a2", "s9", SeatA2)],
            AppendCondition.NothingAppendedFor(TagQuery.AllOf(SeatA1, StudentS7)));

        (await TokenFor(SeatA1)).Should().NotBeNull();
        (await TokenFor(StudentS7)).Should().NotBeNull();
    }

    [Fact]
    public async Task An_intersection_condition_is_refused_when_any_of_its_tag_heads_moves_mid_append()
    {
        // The price of claiming both heads: an event carrying only seat:a1 does not move this
        // boundary, but it does move a row the append is guarding on, so the append is refused.
        // Safe — the retry re-reads the boundary, finds it unmoved and succeeds — and the reason the
        // narrower lock set is a deliberate follow-up rather than an oversight.
        var interceptor = StaleTagHeadInterceptor.OnSameConnection(SeatA1.ToString());
        await using var racing = CreateContext(interceptor);

        var result = await racing.SaveEvents([Reserved("a1", "s7", SeatA1, StudentS7)],
            AppendCondition.NothingAppendedFor(TagQuery.AllOf(SeatA1, StudentS7)));

        interceptor.Fired.Should().BeTrue("the race must actually have been simulated");
        result.IsNotSuccess.Should().BeTrue();
        result.Failure!.Type.Should().Be(EventSourcing.StoreFailures.ConcurrencyConflictType);
    }

    /// <summary>
    /// The companion to <see cref="The_token_is_declared_as_a_concurrency_token"/>. That one covers
    /// the model declaration; this one covers the other half the declaration needs to do anything —
    /// that the head rows are read into the change tracker, so Entity Framework Core has an original
    /// token to guard the update with.
    /// </summary>
    /// <remarks>
    /// Adding <c>AsNoTracking()</c> to that read looks like a free optimisation on rows the append is
    /// about to overwrite anyway. It emits no UPDATE at all: the assignment mutates a detached
    /// object, the check disappears, and the heads stop moving for every other append too. Verified
    /// by doing exactly that.
    /// </remarks>
    [Fact]
    public async Task A_conditioned_append_guards_its_tag_head_update_on_the_token_it_read()
    {
        var interceptor = new CapturingCommandInterceptor();
        await using var capturing = CreateContext(interceptor);

        var result = await capturing.SaveEvents([Reserved("a1", "s7")],
            AppendCondition.NothingAppendedFor(TagQuery.AnyOf(SeatA1)));

        result.IsSuccess.Should().BeTrue("the append itself is uncontended");

        using (new AssertionScope())
        {
            var update = interceptor.TagHeadUpdates.Should()
                .ContainSingle("the one tag in the boundary is claimed exactly once")
                .Subject;

            var where = update.IndexOf("WHERE", StringComparison.Ordinal);
            where.Should().BeGreaterThan(-1, "an unguarded update would claim the row unconditionally");

            update[where..].Should().Contain(nameof(DcbTagHeadEntity.Token),
                "the token read before the write is what the update matches on — without it in the "
                + "WHERE clause the append cannot detect an overlapping one");
        }
    }
}
