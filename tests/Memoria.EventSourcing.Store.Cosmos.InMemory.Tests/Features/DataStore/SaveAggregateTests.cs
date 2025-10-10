using FluentAssertions;
using FluentAssertions.Execution;
using OpenCqrs.EventSourcing.Store.Tests.Models.Aggregates;
using OpenCqrs.EventSourcing.Store.Tests.Models.Streams;
using Xunit;

namespace OpenCqrs.EventSourcing.Store.Cosmos.InMemory.Tests.Features.DataStore;

public class SaveAggregateTests : TestBase
{
    [Fact]
    public async Task GivenNewAggregateSaved_ThenAggregateAndEventDocumentsAreStored()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);
        var aggregate = new TestAggregate1(id, "Test Name", "Test Description");

        var saveResult = await DomainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);
        var aggregateDocument = await DataStore.GetAggregateDocument(streamId, aggregateId);
        var eventDocuments = await DataStore.GetEventDocuments(streamId);

        using (new AssertionScope())
        {
            saveResult.IsSuccess.Should().BeTrue();

            aggregateDocument.Value.Should().NotBeNull();
            aggregateDocument.Value.Id.Should().Be($"{aggregateId.Id}:1");
            aggregateDocument.Value.AggregateType.Should().Be("TestAggregate1:1");
            aggregateDocument.Value.Version.Should().Be(1);
            aggregateDocument.Value.LatestEventSequence.Should().Be(1);

            eventDocuments.Value.Should().NotBeNull();
            eventDocuments.Value[0].Id.Should().Be($"{streamId.Id}:1");
            eventDocuments.Value[0].EventType.Should().Be("TestAggregateCreated:1");
            eventDocuments.Value[0].Sequence.Should().Be(1);
        }
    }

    [Fact]
    public async Task GivenAggregateIsUpdated_ThenAggregateDocumentVersionIncreasesAndAllEventDocumentsAreStored()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);
        var aggregate = new TestAggregate1(id, "Test Name", "Test Description");

        await DomainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);
        var updatedAggregateResult = await DomainService.GetAggregate(streamId, aggregateId);
        aggregate = updatedAggregateResult.Value!;
        aggregate.Update("Updated Name", "Updated Description");

        var saveResult = await DomainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 1);
        var aggregateDocument = await DataStore.GetAggregateDocument(streamId, aggregateId);
        var eventDocuments = await DataStore.GetEventDocuments(streamId);

        using (new AssertionScope())
        {
            saveResult.IsSuccess.Should().BeTrue();

            aggregateDocument.Value.Should().NotBeNull();
            aggregateDocument.Value.AggregateType.Should().Be("TestAggregate1:1");
            aggregateDocument.Value.Version.Should().Be(2);
            aggregateDocument.Value.LatestEventSequence.Should().Be(2);

            eventDocuments.Value!.Count.Should().Be(2);
            eventDocuments.Value[0].Id.Should().Be($"{streamId.Id}:1");
            eventDocuments.Value[0].EventType.Should().Be("TestAggregateCreated:1");
            eventDocuments.Value[0].Sequence.Should().Be(1);
            eventDocuments.Value[1].Id.Should().Be($"{streamId.Id}:2");
            eventDocuments.Value[1].EventType.Should().Be("TestAggregateUpdated:1");
            eventDocuments.Value[1].Sequence.Should().Be(2);
        }
    }

    [Fact]
    public async Task GivenNewAggregateSaved_WhenMultipleEventsAdded_ThenAggregateAndAllEventDocumentsAreStored()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);
        var aggregate = new TestAggregate1(id, "Test Name", "Test Description");
        aggregate.Update("Updated Name", "Updated Description");

        var saveResult = await DomainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);
        var aggregateDocument = await DataStore.GetAggregateDocument(streamId, aggregateId);
        var eventDocuments = await DataStore.GetEventDocuments(streamId);

        using (new AssertionScope())
        {
            saveResult.IsSuccess.Should().BeTrue();

            aggregateDocument.Value.Should().NotBeNull();
            aggregateDocument.Value.AggregateType.Should().Be("TestAggregate1:1");
            aggregateDocument.Value.Version.Should().Be(2);
            aggregateDocument.Value.LatestEventSequence.Should().Be(2);

            eventDocuments.Value!.Count.Should().Be(2);
            eventDocuments.Value[0].Id.Should().Be($"{streamId.Id}:1");
            eventDocuments.Value[0].EventType.Should().Be("TestAggregateCreated:1");
            eventDocuments.Value[0].Sequence.Should().Be(1);
            eventDocuments.Value[1].Id.Should().Be($"{streamId.Id}:2");
            eventDocuments.Value[1].EventType.Should().Be("TestAggregateUpdated:1");
            eventDocuments.Value[1].Sequence.Should().Be(2);
        }
    }

    [Fact]
    public async Task GivenNewAggregateSaved_ThenAuditablePropertiesArePopulated()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);
        var aggregate = new TestAggregate1(id, "Test Name", "Test Description");
        var now = new DateTime(2024, 6, 10, 12, 0, 0, DateTimeKind.Utc);

        TimeProvider.SetUtcNow(now);

        await DomainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);
        var aggregateDocument = await DataStore.GetAggregateDocument(streamId, aggregateId);
        var eventDocuments = await DataStore.GetEventDocuments(streamId);

        using (new AssertionScope())
        {
            aggregateDocument.Value.Should().NotBeNull();
            aggregateDocument.Value.CreatedBy.Should().Be("TestUser");
            aggregateDocument.Value.CreatedDate.Should().Be(now);
            aggregateDocument.Value.UpdatedBy.Should().Be("TestUser");
            aggregateDocument.Value.UpdatedDate.Should().Be(now);

            eventDocuments.Value.Should().NotBeNull();
            eventDocuments.Value[0].Should().NotBeNull();
            eventDocuments.Value[0].CreatedBy.Should().Be("TestUser");
            eventDocuments.Value[0].CreatedDate.Should().Be(now);
        }
    }

    [Fact]
    public async Task GivenAggregateUpdated_ThenAuditablePropertiesArePopulated()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);
        var aggregate = new TestAggregate1(id, "Test Name", "Test Description");

        var createDate = new DateTime(2024, 6, 10, 12, 0, 0, DateTimeKind.Utc);
        TimeProvider.SetUtcNow(createDate);
        await DomainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);

        var updateDate = new DateTime(2024, 6, 15, 18, 0, 0, DateTimeKind.Utc);
        TimeProvider.SetUtcNow(updateDate);
        var aggregateToUpdateResult = await DomainService.GetAggregate(streamId, aggregateId);
        aggregateToUpdateResult.Value!.Update("Updated Name", "Updated Description");
        await DomainService.SaveAggregate(streamId, aggregateId, aggregateToUpdateResult.Value, expectedEventSequence: 1);

        var aggregateDocument = await DataStore.GetAggregateDocument(streamId, aggregateId);
        var eventDocuments = await DataStore.GetEventDocuments(streamId);

        using (new AssertionScope())
        {
            aggregateDocument.Value.Should().NotBeNull();
            aggregateDocument.Value.CreatedBy.Should().Be("TestUser");
            aggregateDocument.Value.CreatedDate.Should().Be(createDate);
            aggregateDocument.Value.UpdatedBy.Should().Be("TestUser");
            aggregateDocument.Value.UpdatedDate.Should().Be(updateDate);

            eventDocuments.Value.Should().NotBeNull();
            eventDocuments.Value[0].CreatedBy.Should().Be("TestUser");
            eventDocuments.Value[0].CreatedDate.Should().Be(createDate);
        }
    }
}
