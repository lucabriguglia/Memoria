using AwesomeAssertions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Relational.Tests.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Relational.Tests.Features;

/// <summary>
/// Changes a caller stages on the context are saved with the append, all or nothing — including
/// when the append introduces a tag that has no head yet.
/// </summary>
/// <remarks>
/// <para>
/// The staged change is a snapshot row added to the context by hand: any tracked entity of the
/// caller's would do, and this one needs no schema of its own.
/// </para>
/// <para>
/// Every append here introduces a new tag. Creating its head used to be a save of the whole context,
/// made before the append's transaction: it committed the staged change whatever the append then
/// decided, and took any failure of it for another append having created the head first.
/// </para>
/// </remarks>
public class StagedChangesTests : RelationalTestBase
{
    private static readonly Tag SeatA1 = new("seat", "a1");
    private static readonly Tag StudentS7 = new("student", "s7");

    private static TaggedEvent Reserved(string student, params Tag[] tags) =>
        new(new SeatReservedEvent("a1", student), tags.Length > 0 ? tags : [SeatA1]);

    private static DcbSnapshotEntity StagedRow(string id, string data = "{}") => new()
    {
        Id = id,
        SnapshotKind = DcbSnapshotEntity.ProjectionKind,
        StoreId = id,
        TagQuery = "staged",
        ModelType = "Staged:1",
        Version = 1,
        LatestPosition = 0,
        Data = data
    };

    [Fact]
    public async Task A_refused_append_introducing_a_tag_saves_no_staged_change()
    {
        await Context.SaveEvents([Reserved("s6")], condition: null);
        Context.DcbSnapshots.Add(StagedRow("staged-1"));

        var result = await Context.SaveEvents([Reserved("s7", SeatA1, StudentS7)],
            AppendCondition.NothingAppendedFor(TagQuery.AnyOf(SeatA1)));

        result.Failure!.Type.Should().Be(StoreFailures.ConcurrencyConflictType);
        Context.ChangeTracker.HasChanges().Should().BeTrue("the caller still holds what it staged");
        await using var reading = CreateContext();
        (await reading.DcbSnapshots.CountAsync()).Should().Be(0, "a refused append commits nothing staged");
    }

    [Fact]
    public async Task A_staged_change_that_cannot_be_saved_fails_an_append_introducing_a_tag()
    {
        await using (var existing = CreateContext())
        {
            existing.DcbSnapshots.Add(StagedRow("staged-1"));
            await existing.SaveChangesAsync();
        }

        Context.DcbSnapshots.Add(StagedRow("staged-1", data: "{\"again\":true}"));

        var result = await Context.SaveEvents([Reserved("s7", SeatA1, StudentS7)],
            AppendCondition.NothingAppendedFor(TagQuery.AnyOf(SeatA1)));

        result.IsNotSuccess.Should().BeTrue("the staged change could not be saved, so neither may the append");
        result.Failure!.Type.Should().Be(StoreFailures.StorageFailureType);
        await using var reading = CreateContext();
        (await reading.DcbEvents.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task An_accepted_append_introducing_a_tag_commits_the_staged_change_with_it()
    {
        Context.DcbSnapshots.Add(StagedRow("staged-1"));

        var result = await Context.SaveEvents([Reserved("s7", SeatA1, StudentS7)],
            AppendCondition.NothingAppendedFor(TagQuery.AnyOf(SeatA1)));

        result.IsSuccess.Should().BeTrue(result.Failure?.Description);
        await using var reading = CreateContext();
        (await reading.DcbSnapshots.CountAsync()).Should().Be(1);
        (await reading.DcbEvents.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task A_refused_aggregate_save_introducing_a_tag_saves_no_staged_change()
    {
        await Context.SaveEvents([Reserved("s6")], condition: null);
        Context.DcbSnapshots.Add(StagedRow("staged-1"));
        var aggregate = new SeatAggregate();
        aggregate.Reserve("a1", "s7");

        var result = await Context.SaveAggregate(new SeatId("a1"), aggregate,
            AppendCondition.NothingAppendedFor(TagQuery.AnyOf(SeatA1)));

        result.Failure!.Type.Should().Be(StoreFailures.ConcurrencyConflictType);
        await using var reading = CreateContext();
        (await reading.DcbSnapshots.CountAsync()).Should().Be(0, "a refused save commits nothing staged");
    }
}
