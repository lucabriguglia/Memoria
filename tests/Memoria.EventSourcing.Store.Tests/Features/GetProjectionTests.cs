using FluentAssertions;
using FluentAssertions.Execution;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.Tests.Models.Events;
using Memoria.EventSourcing.Store.Tests.Models.Projections;
using Memoria.EventSourcing.Store.Tests.Models.Streams;
using Xunit;

namespace Memoria.EventSourcing.Store.Tests.Features;

public abstract class GetProjectionTests(IDomainServiceFactory domainServiceFactory) : TestBase(domainServiceFactory)
{
    [Fact]
    public async Task GivenAProjectionIsSaved_ThenItCanBeRetrievedAsASnapshot()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var projectionId = new TestProjectionId(id);

        var projection = new TestProjection();
        projection.Apply(new IEvent[]
        {
            new TestAggregateCreatedEvent(id, "Test Name", "Test Description"),
            new TestAggregateUpdatedEvent(id, "Updated Name", "Updated Description")
        });

        var saveResult = await DomainService.SaveProjection(streamId, projectionId, projection);
        var getResult = await DomainService.GetProjection(streamId, projectionId);

        using (new AssertionScope())
        {
            saveResult.IsSuccess.Should().BeTrue();

            getResult.IsSuccess.Should().BeTrue();
            getResult.Value.Should().NotBeNull();
            getResult.Value!.StreamId.Should().Be(streamId.Id);
            getResult.Value.ProjectionId.Should().Be(projectionId.ToStoreId());
            getResult.Value.Version.Should().Be(2);
            getResult.Value.EventsApplied.Should().Be(2);
            getResult.Value.Name.Should().Be("Updated Name");
            getResult.Value.Description.Should().Be("Updated Description");
        }
    }

    [Fact]
    public async Task GivenAProjectionIsSavedTwice_ThenTheSnapshotIsUpdated()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var projectionId = new TestProjectionId(id);

        var projection = new TestProjection();
        projection.Apply([new TestAggregateCreatedEvent(id, "Test Name", "Test Description")]);
        await DomainService.SaveProjection(streamId, projectionId, projection);

        projection.Apply([new TestAggregateUpdatedEvent(id, "Updated Name", "Updated Description")]);
        var saveResult = await DomainService.SaveProjection(streamId, projectionId, projection);

        var getResult = await DomainService.GetProjection(streamId, projectionId);

        using (new AssertionScope())
        {
            saveResult.IsSuccess.Should().BeTrue();

            getResult.IsSuccess.Should().BeTrue();
            getResult.Value.Should().NotBeNull();
            getResult.Value!.Version.Should().Be(2);
            getResult.Value.EventsApplied.Should().Be(2);
            getResult.Value.Name.Should().Be("Updated Name");
            getResult.Value.Description.Should().Be("Updated Description");
        }
    }

    [Fact]
    public async Task GivenNoProjectionHasBeenSaved_ThenGetReturnsNull()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var projectionId = new TestProjectionId(id);

        var getResult = await DomainService.GetProjection(streamId, projectionId);

        using (new AssertionScope())
        {
            getResult.IsSuccess.Should().BeTrue();
            getResult.Failure.Should().BeNull();
            getResult.Value.Should().BeNull();
        }
    }

    [Fact]
    public async Task GivenAProjectionIsSaved_ThenAuditablePropertiesAreNotSerializedIntoTheState()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var projectionId = new TestProjectionId(id);

        var projection = new TestProjection();
        projection.Apply([new TestAggregateCreatedEvent(id, "Test Name", "Test Description")]);

        await DomainService.SaveProjection(streamId, projectionId, projection);
        var getResult = await DomainService.GetProjection(streamId, projectionId);

        using (new AssertionScope())
        {
            getResult.IsSuccess.Should().BeTrue();
            getResult.Value.Should().NotBeNull();
            getResult.Value!.Version.Should().Be(1);
            getResult.Value.Name.Should().Be("Test Name");
        }
    }
}
