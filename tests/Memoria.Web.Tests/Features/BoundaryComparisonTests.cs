using AwesomeAssertions;
using Memoria.EventSourcing;
using Memoria.EventSourcing.Dcb;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.EventSourcing.Domain;
using Memoria.Results;
using Memoria.Web.Extensibility;
using NSubstitute;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// The compare tab's one question on a DCB page, asked of the boundary's history and one read of
/// its events together: which two versions, folded up to which positions, differing where. The
/// history is in hand as headers — position, type and date, no payloads — so counting and placing
/// versions is arithmetic over that list, and only one read of the events goes to the store: up to
/// the later version's position, with both versions folded from it.
/// <para>
/// The model here has eight events at positions 7 to 14 of the one log: version one is the fold
/// up to 7, version eight the fold up to 14.
/// </para>
/// </summary>
public class BoundaryComparisonTests
{
    private static readonly SampleCountingDcbAggregateId AggregateId = new("abc-1");

    private static readonly DateTimeOffset Written = new(2026, 5, 6, 11, 15, 0, TimeSpan.Zero);

    private static DcbEventHeader Header(long position) => new(position, "Sample:1", Written.AddMinutes(position), "an operator");

    private static BoundaryHistory History(int events = 8) =>
        new(Enumerable.Range(1, events).Select(n => Header(6 + n)).ToList(), Error: null);

    private static BoundaryComparisonRequest Request(string? from, string? to) =>
        new(typeof(SampleCountingDcbAggregate), AggregateId, from, to);

    /// <summary>
    /// A store holding the model's events up to a position: one event per version, since the model
    /// counts each.
    /// </summary>
    private static IDcbDomainService Holding(long upToPosition, Result<List<IEvent>> events)
    {
        var store = Substitute.For<IDcbDomainService>();

        store.GetEventsUpToPosition(
                Arg.Any<TagQuery>(),
                upToPosition,
                Arg.Any<Type[]?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(events));

        return store;
    }

    private static IDcbDomainService Holding() => Substitute.For<IDcbDomainService>();

    private static List<IEvent> Events(int count) =>
        Enumerable.Range(1, count).Select(n => (IEvent)new SampleHappenedEvent($"event-{n}")).ToList();

    [Fact]
    public async Task Compares_the_last_version_against_the_one_before_it_when_nothing_is_asked_for()
    {
        var store = Holding(14, Events(8));

        var comparison = await BoundaryComparison.Of(History(), store, Request(null, null));

        comparison.LastVersion.Should().Be(8);
        comparison.Range.Should().Be(new CompareRange(7, 8));
        comparison.From!.Sequence.Should().Be(13);
        comparison.To!.Sequence.Should().Be(14);
        comparison.Error.Should().BeNull();
        comparison.Rows.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new DiffRow("Count", "int", "7", "8", Change.Changed, []));
    }

    /// <summary>
    /// The two versions come out of one read of the boundary, up to the later version's position.
    /// The store is never asked to fold, which would read the events a second time.
    /// </summary>
    [Fact]
    public async Task Reads_the_boundary_once_for_both_versions()
    {
        var store = Holding(14, Events(8));

        await BoundaryComparison.Of(History(), store, Request(null, null));

        await store.Received(1).GetEventsUpToPosition(
            AggregateId.Boundary, 14, Arg.Any<Type[]?>(), Arg.Any<CancellationToken>());
        store.ReceivedCalls().Should().HaveCount(1);
    }

    /// <summary>
    /// A version is the model's own count, so the fold for version three stops at the model's third
    /// event — position 9 here — and not at position 3, which is some other model's.
    /// </summary>
    [Fact]
    public async Task Folds_each_version_up_to_the_position_of_the_models_own_event()
    {
        var store = Holding(13, Events(7));

        var comparison = await BoundaryComparison.Of(History(), store, Request("3", "7"));

        comparison.Range.Should().Be(new CompareRange(3, 7));
        comparison.From!.Sequence.Should().Be(9);
        comparison.To!.Sequence.Should().Be(13);
        comparison.Rows.Single().Should().BeEquivalentTo(new DiffRow("Count", "int", "3", "7", Change.Changed, []));
    }

    /// <summary>
    /// The event that produced each version travels with it — its type, when it was appended and by whom —
    /// straight off the header, so the cards say what each version is without a payload being read.
    /// </summary>
    [Fact]
    public async Task Carries_the_event_that_produced_each_version()
    {
        var store = Holding(13, Events(7));

        var comparison = await BoundaryComparison.Of(History(), store, Request("3", "7"));

        comparison.From.Should().Be(new FoldPoint(3, 9, "Sample:1", Written.AddMinutes(9), "an operator"));
        comparison.To.Should().Be(new FoldPoint(7, 13, "Sample:1", Written.AddMinutes(13), "an operator"));
    }

    [Fact]
    public async Task Folds_version_zero_as_the_model_before_anything_happened()
    {
        var store = Holding(7, Events(1));

        var comparison = await BoundaryComparison.Of(History(), store, Request("0", "1"));

        comparison.From.Should().Be(new FoldPoint(0, 0, null, null, null));
        comparison.To!.Sequence.Should().Be(7);
        comparison.Rows.Single().Should().BeEquivalentTo(new DiffRow("Count", "int", "0", "1", Change.Changed, []));
    }

    [Fact]
    public async Task Has_nothing_to_compare_on_a_history_with_no_events()
    {
        var store = Holding();

        var comparison = await BoundaryComparison.Of(History(events: 0), store, Request(null, null));

        comparison.LastVersion.Should().Be(0);
        comparison.Range.Should().BeNull();
        comparison.Error.Should().Contain("no events");
        store.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task Says_why_when_the_history_could_not_be_read()
    {
        var store = Holding();
        var unreadable = new BoundaryHistory([], "The store could not be reached.");

        var comparison = await BoundaryComparison.Of(unreadable, store, Request("3", "7"));

        comparison.LastVersion.Should().BeNull();
        comparison.Error.Should().Contain("could not be reached");
        store.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task Refuses_a_version_past_the_last_without_asking_the_store_to_fold()
    {
        var store = Holding();

        var comparison = await BoundaryComparison.Of(History(), store, Request("3", "9"));

        comparison.Range.Should().BeNull();
        comparison.Error.Should().Contain("8");
        store.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task Reports_a_read_the_store_refused()
    {
        var store = Holding(14,
            (Result<List<IEvent>>)new Failure(ErrorCode.Error, "Boundary unreadable", "Event 14 will not open."));

        var comparison = await BoundaryComparison.Of(History(), store, Request(null, null));

        comparison.Range.Should().Be(new CompareRange(7, 8));
        comparison.Error.Should().Contain("Boundary unreadable");
        comparison.Rows.Should().BeEmpty();
    }

    /// <summary>
    /// Without a rebuilt identifier there is nothing to fold through, and the page says why in
    /// place of the form — so nothing is folded.
    /// </summary>
    [Fact]
    public async Task Asks_nothing_when_there_is_no_identifier()
    {
        var store = Holding();

        var comparison = await BoundaryComparison.Of(History(), store, Request(null, null) with { Identifier = null });

        comparison.Should().Be(ModelComparison.None);
        store.ReceivedCalls().Should().BeEmpty();
    }
}

/// <summary>
/// A DCB aggregate with one property to read, so a comparison has a row to show: how many events
/// it has applied, which is what a version of it is.
/// </summary>
public class SampleCountingDcbAggregate : DcbAggregateRoot
{
    public int Count { get; set; }

    public override Type[]? EventTypeFilter => null;

    protected override bool Apply<T>(T @event)
    {
        Count++;
        return true;
    }
}

public class SampleCountingDcbAggregateId(string id) : IDcbAggregateId<SampleCountingDcbAggregate>
{
    public string Id { get; } = id;

    public TagQuery Boundary { get; } = TagQuery.AnyOf(new Tag("sample", id));
}
