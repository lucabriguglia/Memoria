using System.Diagnostics;
using FluentAssertions;
using FluentAssertions.Execution;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.Tests.Models.Aggregates;
using Memoria.EventSourcing.Store.Tests.Models.Events;
using Memoria.EventSourcing.Store.Tests.Models.Streams;
using Xunit;

namespace Memoria.EventSourcing.Store.Tests.Features;

/// <summary>
/// Every store records what a snapshot write folded, so that a wrong aggregate state can be
/// explained from tracing alone: which event sequences were consumed, and how many of them actually
/// mutated the aggregate.
/// </summary>
public abstract class AggregateFoldedDiagnosticsTests(IDomainServiceFactory domainServiceFactory)
    : TestBase(domainServiceFactory)
{
    private const string AggregateFolded = "Aggregate Folded";

    [Fact]
    public async Task GivenAColdAggregateBuild_ThenTheFoldIsRecordedOnTheCurrentActivity()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);

        var events = new IEvent[]
        {
            new TestAggregateCreatedEvent(id, "Test Name", "Test Description"),
            new TestAggregateUpdatedEvent(id, "Updated Name", "Updated Description")
        };
        (await DomainService.SaveEvents(streamId, events, expectedEventSequence: 0)).ShouldHaveSucceeded();

        (await DomainService.GetAggregate(streamId, aggregateId, ReadMode.SnapshotOrCreate)).ShouldHaveSucceeded();

        var tags = SingleFoldTags();

        using (new AssertionScope())
        {
            tags["streamId"].Should().Be(streamId.Id);
            tags["aggregateId"].Should().Be(aggregateId.ToStoreId());
            tags["appliedFromSequence"].Should().Be(1);
            tags["appliedToSequence"].Should().Be(2);
            tags["appliedCount"].Should().Be(2);
            tags["versionBefore"].Should().Be(0);
            tags["versionAfter"].Should().Be(2);
        }
    }

    /// <summary>
    /// The case the aggregate-event link could never express: an event inside the aggregate's
    /// EventTypeFilter whose Apply ignores it is consumed by the fold but changes nothing.
    /// A debugger needs to see that it was a no-op.
    /// </summary>
    [Fact]
    public async Task GivenAFoldedEventTheAggregateIgnores_ThenAppliedCountExceedsTheVersionDelta()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);

        // SomethingHappenedEvent is in TestAggregate1's EventTypeFilter, but its Apply returns false.
        var events = new IEvent[]
        {
            new TestAggregateCreatedEvent(id, "Test Name", "Test Description"),
            new SomethingHappenedEvent("Something"),
            new TestAggregateUpdatedEvent(id, "Updated Name", "Updated Description")
        };
        (await DomainService.SaveEvents(streamId, events, expectedEventSequence: 0)).ShouldHaveSucceeded();

        (await DomainService.GetAggregate(streamId, aggregateId, ReadMode.SnapshotOrCreate)).ShouldHaveSucceeded();

        var tags = SingleFoldTags();

        using (new AssertionScope())
        {
            tags["appliedCount"].Should().Be(3);
            tags["appliedFromSequence"].Should().Be(1);
            tags["appliedToSequence"].Should().Be(3);
            tags["versionBefore"].Should().Be(0);
            tags["versionAfter"].Should().Be(2);
        }
    }

    [Fact]
    public async Task GivenAnAggregateIsSaved_ThenTheFoldIsRecordedOnTheCurrentActivity()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);
        var aggregate = new TestAggregate1(id, "Test Name", "Test Description");

        (await DomainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0))
            .ShouldHaveSucceeded();

        var tags = SingleFoldTags();

        using (new AssertionScope())
        {
            tags["streamId"].Should().Be(streamId.Id);
            tags["aggregateId"].Should().Be(aggregateId.ToStoreId());
            tags["appliedFromSequence"].Should().Be(1);
            tags["appliedToSequence"].Should().Be(1);
            tags["appliedCount"].Should().Be(1);
            tags["versionBefore"].Should().Be(0);
            tags["versionAfter"].Should().Be(1);
        }
    }

    [Fact]
    public async Task GivenAnAggregateIsUpdatedWithNewEvents_ThenTheFoldRecordsOnlyTheNewEvents()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);
        var aggregate = new TestAggregate1(id, "Test Name", "Test Description");

        (await DomainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0))
            .ShouldHaveSucceeded();

        var events = new IEvent[]
        {
            new TestAggregateUpdatedEvent(id, "Updated Name", "Updated Description")
        };
        (await DomainService.SaveEvents(streamId, events, expectedEventSequence: 1)).ShouldHaveSucceeded();

        (await DomainService.UpdateAggregate(streamId, aggregateId)).ShouldHaveSucceeded();

        // The save above folded too, so this asserts on the refresh: the second and last fold.
        var tags = LastFoldTags();

        using (new AssertionScope())
        {
            tags["appliedFromSequence"].Should().Be(2);
            tags["appliedToSequence"].Should().Be(2);
            tags["appliedCount"].Should().Be(1);
            tags["versionBefore"].Should().Be(1);
            tags["versionAfter"].Should().Be(2);
        }
    }

    /// <summary>
    /// The tag payload must not grow with the stream: a long stream is recorded with the same
    /// bounded scalars as a short one, so tracing cost stays predictable.
    /// </summary>
    [Fact]
    public async Task GivenALongStream_ThenTheFoldRecordsTheSameBoundedTags()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);

        var events = new IEvent[60];
        events[0] = new TestAggregateCreatedEvent(id, "Test Name", "Test Description");
        for (var i = 1; i < events.Length; i++)
        {
            events[i] = new TestAggregateUpdatedEvent(id, $"Name {i}", $"Description {i}");
        }

        (await DomainService.SaveEvents(streamId, events, expectedEventSequence: 0)).ShouldHaveSucceeded();

        (await DomainService.GetAggregate(streamId, aggregateId, ReadMode.SnapshotOrCreate)).ShouldHaveSucceeded();

        var fold = SingleFold();

        using (new AssertionScope())
        {
            fold.Tags.Should().HaveCount(7);
            var tags = ToTagDictionary(fold);
            tags["appliedFromSequence"].Should().Be(1);
            tags["appliedToSequence"].Should().Be(60);
            tags["appliedCount"].Should().Be(60);
            tags["versionAfter"].Should().Be(60);
        }
    }

    private static List<ActivityEvent> Folds() =>
        Activity.Current!.Events.Where(activityEvent => activityEvent.Name == AggregateFolded).ToList();

    private static ActivityEvent SingleFold()
    {
        var folds = Folds();
        folds.Should().HaveCount(1, "one folding operation records exactly one fold");
        return folds[0];
    }

    private static ActivityEvent LastFold()
    {
        var folds = Folds();
        folds.Should().NotBeEmpty("the operation under test should have recorded a fold");
        return folds[^1];
    }

    private static Dictionary<string, object?> SingleFoldTags() => ToTagDictionary(SingleFold());

    private static Dictionary<string, object?> LastFoldTags() => ToTagDictionary(LastFold());

    private static Dictionary<string, object?> ToTagDictionary(ActivityEvent activityEvent) =>
        activityEvent.Tags.ToDictionary(tag => tag.Key, tag => tag.Value);
}
