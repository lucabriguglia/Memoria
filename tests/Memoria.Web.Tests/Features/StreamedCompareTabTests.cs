using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Memoria.Results;
using Memoria.EventSourcing;
using Memoria.EventSourcing.Domain;
using Memoria.Web.Extensibility;
using NSubstitute;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// The cards on a streamed model's compare tab say what each version is in the words the events
/// tab uses: the event that produced it, when it was appended — and, under that, by whom, since a
/// reader matching a card against the database wants everything the row's header holds.
/// </summary>
public class StreamedCompareTabTests
{
    private static readonly DateTimeOffset Written = new(2026, 5, 6, 11, 15, 0, TimeSpan.Zero);

    private static StoredStreamEvent Row(long sequence, string? writtenBy) =>
        new("sample:1", $"sample:1:{sequence}", new StoredEvent(
            sequence,
            "SampleHappened:1",
            Written.AddMinutes(sequence),
            """{"Id":"one"}""",
            [new DomainPropertyValue("Id", "string", "one")],
            Error: null,
            Tags: [])
        {
            WrittenBy = writtenBy
        });

    /// <summary>A stream holding one event of the model's, appended by whoever is named.</summary>
    private static IStreamedReads Holding(string? writtenBy)
    {
        var reads = Substitute.For<IStreamedReads>();

        reads.Model(Arg.Any<StreamedModelAddress>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ReadStreamModel(
                new StoredStreamModel(
                    "sample:1", "sample-1:1", "SampleAggregate:1", Version: 1, Sequence: 1,
                    Data: "{}", Written, CreatedBy: null, Written, UpdatedBy: null),
                Error: null)));
        reads.Count(Arg.Any<StreamedEventFilter>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new EventCount(1, Error: null)));
        reads.At(Arg.Any<StreamedEventFilter>(), 0, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PlacedStreamEvent(Row(1, writtenBy), Error: null)));

        return reads;
    }

    private static IDomainService Folding()
    {
        var store = Substitute.For<IDomainService>();

        store.GetEventsUpToSequence(
                Arg.Any<IStreamId>(), 1, Arg.Any<Type[]?>(), Arg.Any<IDictionary<string, string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<List<IEvent>>>(new List<IEvent> { new SampleHappenedEvent("one") }));

        return store;
    }

    private static string CompareTab => MemoriaWeb.SampleAggregateDetail("compare") + "&from=0&to=1";

    [Fact]
    public async Task A_version_card_says_who_appended_the_event_under_when_it_was_appended()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes().WithReads(Holding("an operator")).WithDomainService(Folding());

        var page = Markup.Plain(await web.Client.GetStringAsync(CompareTab));

        page.Should().Contain("<dt>Appended</dt>").And.Contain("<dt>Appended by</dt>").And.Contain("an operator");
        page.IndexOf("<dt>Appended by</dt>").Should().BeGreaterThan(page.IndexOf("<dt>Appended</dt>"));
    }

    /// <summary>
    /// Audit is a store concern the application may leave switched off, so a row nobody is named
    /// against is an ordinary row rather than a broken one — said the way the page about one
    /// event says it.
    /// </summary>
    [Fact]
    public async Task A_version_nobody_is_named_against_says_so()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes().WithReads(Holding(null)).WithDomainService(Folding());

        var page = Markup.Plain(await web.Client.GetStringAsync(CompareTab));

        page.Should().Contain("<dt>Appended by</dt>").And.Contain("not attributed");
    }
}
