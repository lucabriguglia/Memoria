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

    [Fact]
    public async Task GivenProjectionDoesNotExist_WhenReadModeIsSnapshotOnly_ThenNullIsReturned()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var projectionId = new TestProjectionId(id);

        var events = new IEvent[]
        {
            new TestAggregateCreatedEvent(id, "Test Name", "Test Description"),
            new TestAggregateUpdatedEvent(id, "Updated Name", "Updated Description")
        };
        await DomainService.SaveEvents(streamId, events, expectedEventSequence: 0);

        var getResult = await DomainService.GetProjection(streamId, projectionId, ReadMode.SnapshotOnly);

        using (new AssertionScope())
        {
            getResult.IsSuccess.Should().BeTrue();
            getResult.Failure.Should().BeNull();
            getResult.Value.Should().BeNull();
        }
    }

    [Fact]
    public async Task GivenProjectionDoesNotExist_WhenReadModeIsSnapshotWithNewEvents_ThenNullIsReturned()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var projectionId = new TestProjectionId(id);

        var events = new IEvent[]
        {
            new TestAggregateCreatedEvent(id, "Test Name", "Test Description"),
            new TestAggregateUpdatedEvent(id, "Updated Name", "Updated Description")
        };
        await DomainService.SaveEvents(streamId, events, expectedEventSequence: 0);

        var getResult = await DomainService.GetProjection(streamId, projectionId, ReadMode.SnapshotWithNewEvents);

        using (new AssertionScope())
        {
            getResult.IsSuccess.Should().BeTrue();
            getResult.Failure.Should().BeNull();
            getResult.Value.Should().BeNull();
        }
    }

    [Fact]
    public async Task GivenProjectionDoesNotExist_WhenReadModeIsSnapshotOrCreate_ThenProjectionIsBuiltFromEvents()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var projectionId = new TestProjectionId(id);

        var events = new IEvent[]
        {
            new TestAggregateCreatedEvent(id, "Test Name", "Test Description"),
            new TestAggregateUpdatedEvent(id, "Updated Name", "Updated Description")
        };
        await DomainService.SaveEvents(streamId, events, expectedEventSequence: 0);

        var getResult = await DomainService.GetProjection(streamId, projectionId, ReadMode.SnapshotOrCreate);

        using (new AssertionScope())
        {
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
    public async Task GivenProjectionDoesNotExist_WhenReadModeIsSnapshotOrCreate_ThenBuiltProjectionIsPersisted()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var projectionId = new TestProjectionId(id);

        var events = new IEvent[]
        {
            new TestAggregateCreatedEvent(id, "Test Name", "Test Description"),
            new TestAggregateUpdatedEvent(id, "Updated Name", "Updated Description")
        };
        await DomainService.SaveEvents(streamId, events, expectedEventSequence: 0);

        await DomainService.GetProjection(streamId, projectionId, ReadMode.SnapshotOrCreate);
        var snapshotResult = await DomainService.GetProjection(streamId, projectionId);

        using (new AssertionScope())
        {
            snapshotResult.IsSuccess.Should().BeTrue();
            snapshotResult.Value.Should().NotBeNull();
            snapshotResult.Value!.Version.Should().Be(2);
            snapshotResult.Value.Name.Should().Be("Updated Name");
        }
    }

    [Fact]
    public async Task GivenProjectionDoesNotExist_WhenReadModeIsSnapshotOrCreate_AndNoEventsAreApplied_ThenNullIsReturned()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var projectionId = new TestProjectionId(id);

        var events = new IEvent[]
        {
            new SomethingHappenedEvent(Something: "Something")
        };
        await DomainService.SaveEvents(streamId, events, expectedEventSequence: 0);

        var getResult = await DomainService.GetProjection(streamId, projectionId, ReadMode.SnapshotOrCreate);

        using (new AssertionScope())
        {
            getResult.IsSuccess.Should().BeTrue();
            getResult.Failure.Should().BeNull();
            getResult.Value.Should().BeNull();
        }
    }

    [Fact]
    public async Task GivenProjectionDoesNotExist_WhenReadModeIsSnapshotOrCreate_AndNoEventsAreStored_ThenNullIsReturned()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var projectionId = new TestProjectionId(id);

        var getResult = await DomainService.GetProjection(streamId, projectionId, ReadMode.SnapshotOrCreate);

        using (new AssertionScope())
        {
            getResult.IsSuccess.Should().BeTrue();
            getResult.Failure.Should().BeNull();
            getResult.Value.Should().BeNull();
        }
    }

    [Fact]
    public async Task GivenProjectionExists_WhenReadModeIsSnapshotWithNewEvents_ThenNewEventsAreApplied()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var projectionId = new TestProjectionId(id);

        await DomainService.SaveEvents(streamId, [new TestAggregateCreatedEvent(id, "Test Name", "Test Description")], expectedEventSequence: 0);
        var projection = new TestProjection();
        projection.Apply([new TestAggregateCreatedEvent(id, "Test Name", "Test Description")]);
        projection.LatestEventSequence = 1;
        await DomainService.SaveProjection(streamId, projectionId, projection);

        await DomainService.SaveEvents(streamId, [new TestAggregateUpdatedEvent(id, "Updated Name", "Updated Description")], expectedEventSequence: 1);

        var getResult = await DomainService.GetProjection(streamId, projectionId, ReadMode.SnapshotWithNewEvents);

        using (new AssertionScope())
        {
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
    public async Task GivenProjectionExists_WhenReadModeIsSnapshotWithNewEvents_ThenUpdatedSnapshotIsPersisted()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var projectionId = new TestProjectionId(id);

        await DomainService.SaveEvents(streamId, [new TestAggregateCreatedEvent(id, "Test Name", "Test Description")], expectedEventSequence: 0);
        var projection = new TestProjection();
        projection.Apply([new TestAggregateCreatedEvent(id, "Test Name", "Test Description")]);
        projection.LatestEventSequence = 1;
        await DomainService.SaveProjection(streamId, projectionId, projection);

        await DomainService.SaveEvents(streamId, [new TestAggregateUpdatedEvent(id, "Updated Name", "Updated Description")], expectedEventSequence: 1);

        await DomainService.GetProjection(streamId, projectionId, ReadMode.SnapshotWithNewEvents);
        var snapshotResult = await DomainService.GetProjection(streamId, projectionId);

        using (new AssertionScope())
        {
            snapshotResult.IsSuccess.Should().BeTrue();
            snapshotResult.Value.Should().NotBeNull();
            snapshotResult.Value!.Version.Should().Be(2);
            snapshotResult.Value.Name.Should().Be("Updated Name");
        }
    }

    [Fact]
    public async Task GivenProjectionExists_WhenReadModeIsSnapshotOnly_ThenNewEventsAreNotApplied()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var projectionId = new TestProjectionId(id);

        await DomainService.SaveEvents(streamId, [new TestAggregateCreatedEvent(id, "Test Name", "Test Description")], expectedEventSequence: 0);
        var projection = new TestProjection();
        projection.Apply([new TestAggregateCreatedEvent(id, "Test Name", "Test Description")]);
        projection.LatestEventSequence = 1;
        await DomainService.SaveProjection(streamId, projectionId, projection);

        await DomainService.SaveEvents(streamId, [new TestAggregateUpdatedEvent(id, "Updated Name", "Updated Description")], expectedEventSequence: 1);

        var getResult = await DomainService.GetProjection(streamId, projectionId, ReadMode.SnapshotOnly);

        using (new AssertionScope())
        {
            getResult.IsSuccess.Should().BeTrue();
            getResult.Value.Should().NotBeNull();
            getResult.Value!.Version.Should().Be(1);
            getResult.Value.Name.Should().Be("Test Name");
        }
    }

    [Fact]
    public async Task GivenProjectionDoesNotExist_WhenReadModeIsSnapshotWithNewEventsOrCreate_ThenProjectionIsBuiltFromEvents()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var projectionId = new TestProjectionId(id);

        var events = new IEvent[]
        {
            new TestAggregateCreatedEvent(id, "Test Name", "Test Description"),
            new TestAggregateUpdatedEvent(id, "Updated Name", "Updated Description")
        };
        await DomainService.SaveEvents(streamId, events, expectedEventSequence: 0);

        var getResult = await DomainService.GetProjection(streamId, projectionId, ReadMode.SnapshotWithNewEventsOrCreate);

        using (new AssertionScope())
        {
            getResult.IsSuccess.Should().BeTrue();
            getResult.Value.Should().NotBeNull();
            getResult.Value!.Version.Should().Be(2);
            getResult.Value.EventsApplied.Should().Be(2);
            getResult.Value.Name.Should().Be("Updated Name");
            getResult.Value.Description.Should().Be("Updated Description");
        }
    }

    [Fact]
    public async Task GivenProjectionExists_WhenReadModeIsSnapshotWithNewEventsOrCreate_ThenNewEventsAreApplied()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var projectionId = new TestProjectionId(id);

        await DomainService.SaveEvents(streamId, [new TestAggregateCreatedEvent(id, "Test Name", "Test Description")], expectedEventSequence: 0);
        var projection = new TestProjection();
        projection.Apply([new TestAggregateCreatedEvent(id, "Test Name", "Test Description")]);
        projection.LatestEventSequence = 1;
        await DomainService.SaveProjection(streamId, projectionId, projection);

        await DomainService.SaveEvents(streamId, [new TestAggregateUpdatedEvent(id, "Updated Name", "Updated Description")], expectedEventSequence: 1);

        var getResult = await DomainService.GetProjection(streamId, projectionId, ReadMode.SnapshotWithNewEventsOrCreate);

        using (new AssertionScope())
        {
            getResult.IsSuccess.Should().BeTrue();
            getResult.Value.Should().NotBeNull();
            getResult.Value!.Version.Should().Be(2);
            getResult.Value.Name.Should().Be("Updated Name");
        }
    }

    [Fact]
    public async Task GivenProjectionFilteredByEventType_WhenReadModeIsSnapshotOrCreate_ThenOnlyFilteredEventsAreApplied()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var projectionId = new TestProjectionId(id);

        var events = new IEvent[]
        {
            new TestAggregateCreatedEvent(id, "Test Name", "Test Description"),
            new SomethingHappenedEvent(Something: "Ignored"),
            new TestAggregateUpdatedEvent(id, "Updated Name", "Updated Description")
        };
        await DomainService.SaveEvents(streamId, events, expectedEventSequence: 0);

        var getResult = await DomainService.GetProjection(streamId, projectionId, ReadMode.SnapshotOrCreate);

        using (new AssertionScope())
        {
            getResult.IsSuccess.Should().BeTrue();
            getResult.Value.Should().NotBeNull();
            getResult.Value!.EventsApplied.Should().Be(2);
            getResult.Value.Version.Should().Be(2);
            getResult.Value.Name.Should().Be("Updated Name");
        }
    }
}
