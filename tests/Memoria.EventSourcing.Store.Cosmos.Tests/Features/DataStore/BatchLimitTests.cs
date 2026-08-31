using FluentAssertions;
using FluentAssertions.Execution;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.Tests.Models.Aggregates;
using Memoria.EventSourcing.Store.Tests.Models.Events;
using Memoria.EventSourcing.Store.Tests.Models.Streams;
using Memoria.Results;
using Xunit;

namespace Memoria.EventSourcing.Store.Cosmos.Tests.Features.DataStore;

/// <summary>
/// Behaviour at the Cosmos DB transactional batch ceiling of 100 operations.
/// </summary>
/// <remarks>
/// <para>
/// The store writes several documents per event, so that ceiling turns into a limit on events that
/// callers never asked for. It falls in two places, which are handled differently.
/// </para>
/// <para>
/// <b>Appending</b> — <c>SaveEvents</c> and <c>SaveAggregate</c> must stay atomic with the sequence
/// check that precedes them, so their batches cannot be split. They reject oversized input up front
/// with a failure that names the limit, rather than letting Cosmos DB refuse the batch and
/// reporting it as an ordinary storage failure.
/// </para>
/// <para>
/// <b>Snapshotting</b> — the <c>GetAggregate</c> cold path and <c>UpdateAggregateDocument</c> write
/// a snapshot over events that are already durable, so their writes are split across batches. A
/// failure part-way leaves no snapshot and the next read redoes the work.
/// </para>
/// </remarks>
[Trait("Category", "Emulator")]
public class BatchLimitTests : TestBase
{
    [Fact]
    public async Task GivenExactlyTheMaximumEvents_WhenSaved_ThenTheySucceed()
    {
        var streamId = new TestStreamId(Guid.NewGuid().ToString());

        var result = await DomainService.SaveEvents(streamId, SomethingHappened(100), expectedEventSequence: 0);
        var events = await DomainService.GetEvents(streamId);

        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue();
            events.Value!.Count.Should().Be(100);
        }
    }

    [Fact]
    public async Task GivenMoreThanTheMaximumEvents_WhenSaved_ThenTheLimitIsReportedAndNothingIsWritten()
    {
        var streamId = new TestStreamId(Guid.NewGuid().ToString());

        var result = await DomainService.SaveEvents(streamId, SomethingHappened(101), expectedEventSequence: 0);
        var events = await DomainService.GetEvents(streamId);

        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeFalse();

            // Distinguishable from the store being unreachable: retrying this unchanged cannot help.
            result.Failure!.Type.Should().Be(StoreFailures.BatchLimitExceededType);
            result.Failure.ErrorCode.Should().Be(ErrorCode.BadRequest);
            result.Failure.Description.Should().Contain("101").And.Contain("100");

            events.Value!.Count.Should().Be(0);
        }
    }

    [Fact]
    public async Task GivenExactlyTheMaximumUncommittedEvents_WhenTheAggregateIsSaved_ThenItSucceeds()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);

        // One document per event plus the aggregate document, so 99 events fill the batch exactly.
        var aggregate = AggregateWithUncommittedEvents(id, 99);

        var result = await DomainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);
        var events = await DomainService.GetEvents(streamId);

        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue();
            events.Value!.Count.Should().Be(99);
        }
    }

    [Fact]
    public async Task GivenMoreThanTheMaximumUncommittedEvents_WhenTheAggregateIsSaved_ThenTheLimitIsReported()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);
        var aggregate = AggregateWithUncommittedEvents(id, 100);

        var result = await DomainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);
        var events = await DomainService.GetEvents(streamId);

        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeFalse();
            result.Failure!.Type.Should().Be(StoreFailures.BatchLimitExceededType);
            result.Failure.ErrorCode.Should().Be(ErrorCode.BadRequest);
            result.Failure.Description.Should().Contain("100").And.Contain("99");

            events.Value!.Count.Should().Be(0);
        }
    }

    [Fact]
    public async Task GivenAStreamPastTheBatchCeiling_WhenTheAggregateIsFirstBuilt_ThenTheSnapshotIsWritten()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);
        await SaveAggregateEvents(streamId, id, 150);

        var result = await DomainService.GetAggregate(streamId, aggregateId, ReadMode.SnapshotOrCreate);

        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue();
            result.Value!.Version.Should().Be(150);
            result.Value.LatestEventSequence.Should().Be(150);
        }

        // Written, not just returned: reading the snapshot alone must now find it.
        var snapshot = await DomainService.GetAggregate(streamId, aggregateId);
        snapshot.Value!.Version.Should().Be(150);
    }

    [Fact]
    public async Task GivenASnapshotAndManyNewEvents_WhenTheAggregateIsRefreshed_ThenItCatchesUp()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);

        await SaveAggregateEvents(streamId, id, 10);
        await DomainService.GetAggregate(streamId, aggregateId, ReadMode.SnapshotOrCreate);

        await SaveAggregateEvents(streamId, id, 150, startingSequence: 10);

        var result = await DomainService.GetAggregate(streamId, aggregateId, ReadMode.SnapshotWithNewEvents);

        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue();
            result.Value!.Version.Should().Be(160);
            result.Value.LatestEventSequence.Should().Be(160);
        }
    }

    [Fact]
    public async Task GivenTheSnapshotIsRebuiltOverTheSameEvents_ThenTheRebuildSucceeds()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);
        await SaveAggregateEvents(streamId, id, 150);

        // Driven through the data store rather than GetAggregate, because passing no current
        // document rebuilds from sequence 1 every time. Going through GetAggregate a second time
        // would find the snapshot already current, return early, and write nothing — proving
        // nothing about rewriting the snapshot.
        var first = await DataStore.UpdateAggregateDocument(streamId, aggregateId, aggregateDocument: null);

        // Same document id as the first build. The write is an upsert, so a rebuild replaces the
        // snapshot rather than colliding with it — a CreateItem would reject the second attempt as
        // a conflict, turning a transient failure into one no retry could ever clear.
        var rebuild = await DataStore.UpdateAggregateDocument(streamId, aggregateId, aggregateDocument: null);

        using (new AssertionScope())
        {
            first.IsSuccess.Should().BeTrue();
            rebuild.IsSuccess.Should().BeTrue();
            rebuild.Value!.Version.Should().Be(150);
        }
    }

    private static IEvent[] SomethingHappened(int count) =>
        Enumerable.Range(1, count).Select(IEvent (i) => new SomethingHappenedEvent($"Event {i}")).ToArray();

    private static TestAggregate1 AggregateWithUncommittedEvents(string id, int count)
    {
        var aggregate = new TestAggregate1(id, "Test Name", "Test Description");
        for (var i = 1; i < count; i++)
        {
            aggregate.Update($"Name {i}", $"Description {i}");
        }

        return aggregate;
    }

    private async Task SaveAggregateEvents(IStreamId streamId, string id, int count, int startingSequence = 0)
    {
        const int chunkSize = 40;

        for (var saved = 0; saved < count; saved += chunkSize)
        {
            var take = Math.Min(chunkSize, count - saved);
            var events = Enumerable
                .Range(saved + 1, take)
                .Select(IEvent (i) => startingSequence == 0 && i == 1
                    ? new TestAggregateCreatedEvent(id, "Test Name", "Test Description")
                    : new TestAggregateUpdatedEvent(id, $"Name {i}", $"Description {i}"))
                .ToArray();

            var result = await DomainService.SaveEvents(streamId, events,
                expectedEventSequence: startingSequence + saved);
            result.IsSuccess.Should().BeTrue("the stream should accept the seeded events");
        }
    }
}
