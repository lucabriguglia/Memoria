using FluentAssertions;
using FluentAssertions.Execution;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.Tests.Models.Events;
using Memoria.EventSourcing.Store.Tests.Models.Streams;
using Xunit;

namespace Memoria.EventSourcing.Store.Cosmos.Tests.Features.DataStore;

/// <summary>
/// Reads of a stream far larger than anything else in the suite writes.
/// </summary>
/// <remarks>
/// <para>
/// Every read drains a feed iterator into a list, and how many round trips that takes is the
/// service's choice. Nothing else here writes more than a handful of events, so that drain loop was
/// never exercised against a stream big enough to make the choice interesting. These tests pin the
/// part that must hold either way: the stream comes back whole and in sequence order, however many
/// pages the service decides to use.
/// </para>
/// <para>
/// They deliberately do <em>not</em> assert a page count. Measured against the emulator, an unset
/// <c>MaxItemCount</c> returns all 150 events in one round trip; only an explicit
/// <c>MaxItemCount = 100</c> splits them. Asserting a count would pin a service decision rather than
/// this store's behaviour.
/// </para>
/// <para>
/// Cosmos-only rather than in the shared store suite: this covers the Cosmos feed iterator, and
/// writing this many events against every provider would cost far more than it proves.
/// </para>
/// </remarks>
[Trait("Category", "Emulator")]
public class LargeStreamReadTests : TestBase
{
    // Comfortably past the 100-item REST default, so the service has a real choice to make.
    private const int EventCount = 150;

    // A transactional batch caps at 100 operations and SaveEvents writes one document per event, so
    // the stream is built in chunks that stay under it.
    private const int SaveChunkSize = 50;

    [Fact]
    public async Task GivenALargeStream_WhenAllEventsAreRequested_ThenEveryEventIsReturnedInSequenceOrder()
    {
        var streamId = new TestStreamId(Guid.NewGuid().ToString());
        await SaveEvents(streamId, EventCount);

        var events = await DomainService.GetEvents(streamId);

        using (new AssertionScope())
        {
            events.IsSuccess.Should().BeTrue();
            events.Value!.Count.Should().Be(EventCount);
            events.Value
                .Cast<SomethingHappenedEvent>()
                .Select(@event => @event.Something)
                .Should()
                .BeEquivalentTo(
                    Enumerable.Range(1, EventCount).Select(sequence => $"Event {sequence}"),
                    options => options.WithStrictOrdering());
        }
    }

    [Fact]
    public async Task GivenALargeStream_WhenTheLatestSequenceIsRequested_ThenTheLastSequenceIsReturned()
    {
        var streamId = new TestStreamId(Guid.NewGuid().ToString());
        await SaveEvents(streamId, EventCount);

        var latestEventSequence = await DomainService.GetLatestEventSequence(streamId);

        latestEventSequence.Value.Should().Be(EventCount);
    }

    [Fact]
    public async Task GivenALargeStream_WhenAMidStreamRangeIsRequested_ThenOnlyThatRangeIsReturned()
    {
        var streamId = new TestStreamId(Guid.NewGuid().ToString());
        await SaveEvents(streamId, EventCount);

        var events = await DomainService.GetEventsBetweenSequences(streamId, fromSequence: 90, toSequence: 110);

        using (new AssertionScope())
        {
            events.Value!.Count.Should().Be(21);
            events.Value.Cast<SomethingHappenedEvent>().First().Something.Should().Be("Event 90");
            events.Value.Cast<SomethingHappenedEvent>().Last().Something.Should().Be("Event 110");
        }
    }

    private async Task SaveEvents(IStreamId streamId, int count)
    {
        for (var saved = 0; saved < count; saved += SaveChunkSize)
        {
            var events = Enumerable
                .Range(saved + 1, Math.Min(SaveChunkSize, count - saved))
                .Select(sequence => new SomethingHappenedEvent($"Event {sequence}"))
                .Cast<IEvent>()
                .ToArray();

            var result = await DomainService.SaveEvents(streamId, events, expectedEventSequence: saved);
            result.IsSuccess.Should().BeTrue($"the stream should accept events {saved + 1}..{saved + events.Length}");
        }
    }
}
