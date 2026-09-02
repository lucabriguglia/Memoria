using FluentAssertions;
using FluentAssertions.Execution;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.Cosmos.Documents;
using Memoria.EventSourcing.Store.Tests.Models.Aggregates;
using Memoria.EventSourcing.Store.Tests.Models.Streams;
using Xunit;

namespace Memoria.EventSourcing.Store.Cosmos.Tests.Features.DataStore;

/// <summary>
/// Events, aggregates and projections share one container and one partition key, and their document
/// identifiers are built from different things. Nothing stops two of them wanting the same id.
/// </summary>
/// <remarks>
/// <para>
/// An event is <c>{streamId}:{sequence}</c> and an aggregate is <c>{aggregateId}:{typeVersion}</c>,
/// so an aggregate named after its own stream lands a version 1 snapshot on the id of the event at
/// sequence 1. One aggregate per stream under a shared identifier is a common enough convention that
/// this is reachable by accident.
/// </para>
/// <para>
/// The rest of the suite never met it because <see cref="TestStreamId"/> renders <c>test:{id}</c>
/// while every aggregate id renders <c>test-aggregate-…:{id}</c> — the collision is avoided by naming
/// convention, not by design. The InMemory Cosmos store cannot meet it at all: it keeps each document
/// kind in its own dictionary, so it does not model the one thing that makes this possible.
/// </para>
/// </remarks>
[Trait("Category", "Emulator")]
public class DocumentIdCollisionTests : TestBase
{
    /// <summary>An aggregate id that renders exactly what <see cref="TestStreamId"/> renders.</summary>
    private class CollidingAggregateId(string id) : IAggregateId<TestAggregate1>
    {
        public string Id => $"test:{id}";

        public IDictionary<string, string>? EventPropertyFilter => null;
    }

    [Fact]
    public async Task GivenAnAggregateIdColliding_WhenTheAggregateIsRead_ThenTheCollisionIsReported()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var collidingId = new CollidingAggregateId(id);

        // The aggregate's document id is now exactly the first event's.
        collidingId.ToStoreId().Should().Be($"{streamId.Id}:1");

        await DomainService.SaveEvents(streamId,
            [new Store.Tests.Models.Events.TestAggregateCreatedEvent(id, "Name", "Description")],
            expectedEventSequence: 0);

        var result = await DomainService.GetAggregate(streamId, collidingId, ReadMode.SnapshotOnly);

        using (new AssertionScope())
        {
            result.IsNotSuccess.Should().BeTrue("the event at that id is not an aggregate document");
            result.Failure!.Type.Should().Be(CosmosStoreFailures.DocumentIdCollisionType);
            result.Failure.Description.Should().Contain(DocumentType.Event,
                "the message has to name what is actually stored there to be actionable");
        }
    }

    /// <summary>
    /// The reason this is reported rather than treated as a missing snapshot. Reading it as absent
    /// would rebuild the aggregate from the stream and then upsert the snapshot onto the event.
    /// </summary>
    [Fact]
    public async Task GivenAnAggregateIdColliding_WhenTheAggregateIsSaved_ThenTheEventSurvives()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var collidingId = new CollidingAggregateId(id);

        await DomainService.SaveEvents(streamId,
            [new Store.Tests.Models.Events.TestAggregateCreatedEvent(id, "Name", "Description")],
            expectedEventSequence: 0);

        var aggregate = new TestAggregate1(id, "Other Name", "Other Description");
        var saveResult = await DomainService.SaveAggregate(streamId, collidingId, aggregate,
            expectedEventSequence: 1);

        var events = await DataStore.GetEventDocuments(streamId);

        using (new AssertionScope())
        {
            saveResult.IsNotSuccess.Should().BeTrue("the save would have written over the event");
            saveResult.Failure!.Type.Should().Be(CosmosStoreFailures.DocumentIdCollisionType);

            events.Value.Should().ContainSingle();
            events.Value![0].DocumentType.Should().Be(DocumentType.Event,
                "the event is still an event, not a snapshot wearing its id");
            events.Value[0].Sequence.Should().Be(1);
        }
    }

    [Fact]
    public async Task GivenNoCollision_ThenTheDocumentTypeStillRoundTrips()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);
        var aggregate = new TestAggregate1(id, "Name", "Description");

        await DomainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);

        var aggregateDocument = await DataStore.GetAggregateDocument(streamId, aggregateId);
        var eventDocuments = await DataStore.GetEventDocuments(streamId);

        using (new AssertionScope())
        {
            // The guard is only as good as the field it reads, and that field is written by the
            // serializer rather than by any code here.
            aggregateDocument.Value!.DocumentType.Should().Be(DocumentType.Aggregate);
            eventDocuments.Value![0].DocumentType.Should().Be(DocumentType.Event);
        }
    }
}
