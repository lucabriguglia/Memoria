using System.Diagnostics;
using FluentAssertions;
using FluentAssertions.Execution;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.Tests.Models.Events;
using Memoria.EventSourcing.Store.Tests.Models.Projections;
using Memoria.EventSourcing.Store.Tests.Models.Streams;
using Xunit;

namespace Memoria.EventSourcing.Store.Tests.Features;

/// <summary>
/// Every store records what a projection snapshot write folded, for the same reason it does for an
/// aggregate: a read model in a state nobody expects raises the same question, and it can only be
/// answered from tracing if the answer was written down at the moment of the fold.
/// </summary>
/// <remarks>
/// The event is named <c>Projection Folded</c>, not <c>Aggregate Folded</c>. The tag shapes match
/// apart from the identifier, so a query across both models is a two-name filter — but calling a
/// projection fold an aggregate fold would make that name wrong about half its occurrences.
/// </remarks>
public abstract class ProjectionFoldedDiagnosticsTests(IDomainServiceFactory domainServiceFactory)
    : TestBase(domainServiceFactory)
{
    private const string ProjectionFolded = "Projection Folded";

    [Fact]
    public async Task GivenAColdProjectionBuild_ThenTheFoldIsRecordedOnTheCurrentActivity()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var projectionId = new TestProjectionId(id);

        var events = new IEvent[]
        {
            new TestAggregateCreatedEvent(id, "Test Name", "Test Description"),
            new TestAggregateUpdatedEvent(id, "Updated Name", "Updated Description")
        };
        (await DomainService.SaveEvents(streamId, events, expectedEventSequence: 0)).ShouldHaveSucceeded();

        (await DomainService.GetProjection(streamId, projectionId, ReadMode.SnapshotOrCreate))
            .ShouldHaveSucceeded();

        var tags = SingleFoldTags();

        using (new AssertionScope())
        {
            tags["streamId"].Should().Be(streamId.Id);
            tags["projectionId"].Should().Be(projectionId.ToStoreId());
            tags["appliedFromSequence"].Should().Be(1);
            tags["appliedToSequence"].Should().Be(2);
            tags["appliedCount"].Should().Be(2);
            tags["versionBefore"].Should().Be(0);
            tags["versionAfter"].Should().Be(2);
        }
    }

    [Fact]
    public async Task GivenAProjectionIsUpdatedWithNewEvents_ThenTheFoldRecordsOnlyTheNewEvents()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var projectionId = new TestProjectionId(id);

        var events = new IEvent[] { new TestAggregateCreatedEvent(id, "Test Name", "Test Description") };
        (await DomainService.SaveEvents(streamId, events, expectedEventSequence: 0)).ShouldHaveSucceeded();

        (await DomainService.GetProjection(streamId, projectionId, ReadMode.SnapshotOrCreate))
            .ShouldHaveSucceeded();

        var moreEvents = new IEvent[]
        {
            new TestAggregateUpdatedEvent(id, "Updated Name", "Updated Description")
        };
        (await DomainService.SaveEvents(streamId, moreEvents, expectedEventSequence: 1)).ShouldHaveSucceeded();

        (await DomainService.UpdateProjection(streamId, projectionId)).ShouldHaveSucceeded();

        // The cold build above folded too, so this asserts on the refresh: the second and last fold.
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
    /// The tag payload must not grow with the stream, exactly as it must not for an aggregate.
    /// </summary>
    [Fact]
    public async Task GivenALongStream_ThenTheFoldRecordsTheSameBoundedTags()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var projectionId = new TestProjectionId(id);

        var events = new IEvent[60];
        events[0] = new TestAggregateCreatedEvent(id, "Test Name", "Test Description");
        for (var index = 1; index < events.Length; index++)
        {
            events[index] = new TestAggregateUpdatedEvent(id, $"Name {index}", $"Description {index}");
        }

        (await DomainService.SaveEvents(streamId, events, expectedEventSequence: 0)).ShouldHaveSucceeded();

        (await DomainService.GetProjection(streamId, projectionId, ReadMode.SnapshotOrCreate))
            .ShouldHaveSucceeded();

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

    [Fact]
    public async Task GivenASnapshotOnlyRead_ThenNothingIsFolded()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var projectionId = new TestProjectionId(id);

        var events = new IEvent[] { new TestAggregateCreatedEvent(id, "Test Name", "Test Description") };
        (await DomainService.SaveEvents(streamId, events, expectedEventSequence: 0)).ShouldHaveSucceeded();

        (await DomainService.GetProjection(streamId, projectionId)).ShouldHaveSucceeded();

        Folds().Should().BeEmpty("this read mode folds nothing, so there is nothing to record");
    }

    private static List<ActivityEvent> Folds() =>
        Activity.Current!.Events.Where(activityEvent => activityEvent.Name == ProjectionFolded).ToList();

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
