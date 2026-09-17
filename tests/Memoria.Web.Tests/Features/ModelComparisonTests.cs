using AwesomeAssertions;
using Memoria.EventSourcing;
using Memoria.EventSourcing.Domain;
using Memoria.Results;
using Memoria.Web.Extensibility;
using NSubstitute;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// The compare tab's one question, asked of the store's reads and folds together: which two
/// versions, folded up to which sequences, differing where. What is pinned is what the tab compares
/// when the address names nothing, that a pair of versions is checked against the model's last
/// before the store is asked to fold anything, how a version is turned into the sequence the fold
/// stops at, and that each way the store can decline is carried through as the reason the tab has
/// no table.
/// <para>
/// The model here has eight events in a stream of fourteen, at sequences 7 to 14: version one is
/// the fold up to 7, version eight the fold up to 14. A version is the model's own count, so the
/// six earlier sequences — another model's — do not move it.
/// </para>
/// <para>
/// Asked with the two reads made for it: a count, which is one round trip with no rows, and the
/// event at a place in the history, which is one row with no count. A page of one would answer
/// either, at twice the cost.
/// </para>
/// </summary>
public class ModelComparisonTests
{
    private static readonly SampleStreamId Stream = new("sample-1");

    private static readonly SampleCountingAggregateId AggregateId = new("abc-1");

    private static readonly StreamedIdentity Identity = new(
        typeof(SampleStreamId), Stream, typeof(SampleCountingAggregateId), AggregateId, Values: null);

    private const int Versions = 8;

    private static readonly DateTimeOffset Written = new(2026, 5, 6, 11, 15, 0, TimeSpan.Zero);

    /// <summary>The sequence a version of this model was folded up to.</summary>
    private static long SequenceOf(int version) => 6 + version;

    private static ComparisonRequest Request(string? from, string? to) =>
        new(typeof(SampleCountingAggregate), Identity, StreamId: "sample-1", EventTypes: null, from, to);

    private static StoredEvent Event(long position) =>
        new(position, "Sample:1", Written.AddMinutes(position), "{}", [], null, []) { WrittenBy = "an operator" };

    /// <summary>
    /// A history of the model's events: a count, and the event at any place in it oldest first —
    /// which is how a version is turned into a sequence.
    /// </summary>
    private static IStreamedReads History(int? versions = Versions)
    {
        var reads = Substitute.For<IStreamedReads>();

        reads.Count(Arg.Any<StreamedEventFilter>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(versions is { } known
                ? new EventCount(known, null)
                : new EventCount(null, "The store could not be reached.")));

        reads.At(Arg.Any<StreamedEventFilter>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var index = call.ArgAt<int>(1);

                if (versions is null)
                {
                    return Task.FromResult(new PlacedStreamEvent(null, "The store could not be reached."));
                }

                if (index >= versions)
                {
                    return Task.FromResult(new PlacedStreamEvent(null, null));
                }

                var position = SequenceOf(index + 1);

                return Task.FromResult(new PlacedStreamEvent(
                    new StoredStreamEvent("sample-1", $"sample-1:{position}", Event(position)), null));
            });

        return reads;
    }

    /// <summary>
    /// A store holding the model's events up to a sequence: one event per version, since the model
    /// counts each. Both versions are folded from the one read up to the later sequence, so that is
    /// the only read a store need answer.
    /// </summary>
    private static IDomainService Holding(int upToSequence, Result<List<IEvent>> events)
    {
        var store = Substitute.For<IDomainService>();

        store.GetEventsUpToSequence(
                Arg.Any<IStreamId>(),
                upToSequence,
                Arg.Any<Type[]?>(),
                Arg.Any<IDictionary<string, string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(events));

        return store;
    }

    private static IDomainService Holding() => Substitute.For<IDomainService>();

    private static List<IEvent> Events(int count) =>
        Enumerable.Range(1, count).Select(n => (IEvent)new SampleHappenedEvent($"event-{n}")).ToList();

    [Fact]
    public async Task Compares_the_last_version_against_the_one_before_it_when_nothing_is_asked_for()
    {
        var store = Holding(14, Events(8));

        var comparison = await ModelComparison.Of(History(), store, Request(null, null));

        comparison.LastVersion.Should().Be(8);
        comparison.Range.Should().Be(new CompareRange(7, 8));
        comparison.From!.Sequence.Should().Be(13);
        comparison.To!.Sequence.Should().Be(14);
        comparison.Error.Should().BeNull();
        comparison.Rows.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new DiffRow("Count", "int", "7", "8", Change.Changed, []));
    }

    /// <summary>
    /// The two versions come out of one read of the stream, up to the later version's sequence:
    /// the earlier version is the first of those events folded, and the later is all of them. The
    /// store is never asked to fold, which would read the events a second time.
    /// </summary>
    [Fact]
    public async Task Reads_the_stream_once_for_both_versions()
    {
        var store = Holding(14, Events(8));

        await ModelComparison.Of(History(), store, Request(null, null));

        await store.Received(1).GetEventsUpToSequence(
            Stream, 14, Arg.Any<Type[]?>(), Arg.Any<IDictionary<string, string>?>(), Arg.Any<CancellationToken>());
        store.ReceivedCalls().Should().HaveCount(1);
    }

    /// <summary>
    /// The model's own history is what is counted: the same stream, types and properties the
    /// events tab is read with, so on a shared stream the count is this model's and not the
    /// stream's — and counted, not paged.
    /// </summary>
    [Fact]
    public async Task Counts_the_models_own_history()
    {
        var reads = History();
        var request = Request("3", "7") with { EventTypes = ["Sample:1"] };

        await ModelComparison.Of(reads, Holding(13, Events(7)), request);

        await reads.Received(1).Count(
            Arg.Is<StreamedEventFilter>(filter =>
                filter.StreamPattern == "sample-1" &&
                filter.EventTypes!.SequenceEqual(new[] { "Sample:1" }) &&
                filter.Properties != null),
            Arg.Any<CancellationToken>());
        await reads.DidNotReceive().Events(Arg.Any<StreamedEventFilter>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A version is the model's own count, so the fold for version three stops at the model's third
    /// event — sequence 9 here — and not at sequence 3, which is another model's. Placed by asking
    /// for the event at that index, oldest first, rather than for a page of one.
    /// </summary>
    [Fact]
    public async Task Folds_each_version_up_to_the_sequence_of_the_models_own_event()
    {
        var reads = History();
        var store = Holding(13, Events(7));

        var comparison = await ModelComparison.Of(reads, store, Request("3", "7"));

        comparison.Range.Should().Be(new CompareRange(3, 7));
        comparison.From!.Sequence.Should().Be(9);
        comparison.To!.Sequence.Should().Be(13);
        comparison.Rows.Single().Should().BeEquivalentTo(new DiffRow("Count", "int", "3", "7", Change.Changed, []));

        await reads.Received(1).At(Arg.Is<StreamedEventFilter>(filter => !filter.Descending), 2, Arg.Any<CancellationToken>());
        await reads.Received(1).At(Arg.Is<StreamedEventFilter>(filter => !filter.Descending), 6, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The event that produced each version travels with it — its type, when it was appended and by whom —
    /// so the tab can say what each version is in the words the events tab uses, without a third
    /// read to find out.
    /// </summary>
    [Fact]
    public async Task Carries_the_event_that_produced_each_version()
    {
        var store = Holding(13, Events(7));

        var comparison = await ModelComparison.Of(History(), store, Request("3", "7"));

        comparison.From.Should().Be(new FoldPoint(3, 9, "Sample:1", Written.AddMinutes(9), "an operator"));
        comparison.To.Should().Be(new FoldPoint(7, 13, "Sample:1", Written.AddMinutes(13), "an operator"));
    }

    /// <summary>
    /// Version zero is the model before anything happened to it: no event to look up, and none of
    /// the events read are folded into it.
    /// </summary>
    [Fact]
    public async Task Folds_version_zero_as_the_model_before_anything_happened()
    {
        var store = Holding(7, Events(1));

        var comparison = await ModelComparison.Of(History(), store, Request("0", "1"));

        comparison.From.Should().Be(new FoldPoint(0, 0, null, null, null));
        comparison.To!.Sequence.Should().Be(7);
        comparison.Rows.Single().Should().BeEquivalentTo(new DiffRow("Count", "int", "0", "1", Change.Changed, []));
    }

    [Fact]
    public async Task Has_nothing_to_compare_on_a_history_with_no_events()
    {
        var store = Holding();

        var comparison = await ModelComparison.Of(History(versions: 0), store, Request(null, null));

        comparison.LastVersion.Should().Be(0);
        comparison.Range.Should().BeNull();
        comparison.Error.Should().Contain("no events");
        store.ReceivedCalls().Should().BeEmpty();
    }

    /// <summary>
    /// A count that failed says nothing rather than nothing found: there is no last version to
    /// default to and nothing to bound a pair by, and with the history unreadable a version has no
    /// sequence to fold up to either — so the tab says why rather than folding the wrong thing.
    /// </summary>
    [Fact]
    public async Task Says_why_when_the_history_could_not_be_read()
    {
        var store = Holding();

        var asked = await ModelComparison.Of(History(versions: null), store, Request("3", "7"));
        var unasked = await ModelComparison.Of(History(versions: null), store, Request(null, null));

        asked.LastVersion.Should().BeNull();
        asked.Error.Should().NotBeNullOrWhiteSpace();
        unasked.Error.Should().NotBeNullOrWhiteSpace();
        store.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task Refuses_a_version_past_the_last_without_asking_the_store_to_fold()
    {
        var store = Holding();

        var comparison = await ModelComparison.Of(History(), store, Request("3", "9"));

        comparison.Range.Should().BeNull();
        comparison.Error.Should().Contain("8");
        store.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task Reports_a_read_the_store_refused()
    {
        var store = Holding(14,
            (Result<List<IEvent>>)new Failure(ErrorCode.Error, "Stream unreadable", "Event 14 will not open."));

        var comparison = await ModelComparison.Of(History(), store, Request(null, null));

        comparison.Range.Should().Be(new CompareRange(7, 8));
        comparison.Error.Should().Contain("Stream unreadable");
        comparison.Rows.Should().BeEmpty();
    }

    /// <summary>
    /// Without a rebuilt stream and identifier there is nothing to fold through, and the panel says
    /// which half is missing — so nothing is read and nothing is folded.
    /// </summary>
    [Fact]
    public async Task Asks_nothing_when_the_identity_was_not_rebuilt()
    {
        var reads = History();
        var store = Holding();
        var request = Request(null, null) with { Identity = StreamedIdentity.Unknown };

        var comparison = await ModelComparison.Of(reads, store, request);

        comparison.Should().Be(ModelComparison.None);
        reads.ReceivedCalls().Should().BeEmpty();
        store.ReceivedCalls().Should().BeEmpty();
    }
}

/// <summary>
/// An aggregate with one property to read, so a comparison has a row to show: how many events it
/// has applied, which is what a version of it is.
/// </summary>
public class SampleCountingAggregate : AggregateRoot
{
    public int Count { get; set; }

    public override Type[]? EventTypeFilter => null;

    protected override bool Apply<T>(T @event)
    {
        Count++;
        return true;
    }
}

public class SampleCountingAggregateId(string id) : IAggregateId<SampleCountingAggregate>
{
    public string Id { get; } = id;

    public IDictionary<string, string>? EventPropertyFilter => null;
}
