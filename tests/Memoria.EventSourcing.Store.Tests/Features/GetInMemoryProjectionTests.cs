using FluentAssertions;
using FluentAssertions.Execution;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.Tests.Models.Events;
using Memoria.EventSourcing.Store.Tests.Models.Projections;
using Memoria.EventSourcing.Store.Tests.Models.Streams;
using Xunit;

namespace Memoria.EventSourcing.Store.Tests.Features;

public abstract class GetInMemoryProjectionTests(IDomainServiceFactory domainServiceFactory) : TestBase(domainServiceFactory)
{
    [Fact]
    public async Task GivenEventsHandledByTheProjectionTypeAreStored_ThenTheInMemoryProjectionIsReturned()
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

        var getProjectionResult = await DomainService.GetInMemoryProjection(streamId, projectionId);

        using (new AssertionScope())
        {
            getProjectionResult.IsSuccess.Should().BeTrue();

            getProjectionResult.Value.Should().NotBeNull();
            getProjectionResult.Value.StreamId.Should().Be(streamId.Id);
            getProjectionResult.Value.ProjectionId.Should().Be(projectionId.ToStoreId());
            getProjectionResult.Value.Version.Should().Be(2);
            getProjectionResult.Value.LatestEventSequence.Should().Be(2);
            getProjectionResult.Value.EventsApplied.Should().Be(2);
            getProjectionResult.Value.Name.Should().Be("Updated Name");
            getProjectionResult.Value.Description.Should().Be("Updated Description");
        }
    }

    [Fact]
    public async Task GivenEventsHandledByTheProjectionTypeAreStored_ThenTheInMemoryProjectionUpToSequenceIsReturned()
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

        var getProjectionResult = await DomainService.GetInMemoryProjection(streamId, projectionId, upToSequence: 1);

        using (new AssertionScope())
        {
            getProjectionResult.IsSuccess.Should().BeTrue();

            getProjectionResult.Value.Should().NotBeNull();
            getProjectionResult.Value.StreamId.Should().Be(streamId.Id);
            getProjectionResult.Value.ProjectionId.Should().Be(projectionId.ToStoreId());
            getProjectionResult.Value.Version.Should().Be(1);
            getProjectionResult.Value.LatestEventSequence.Should().Be(1);
            getProjectionResult.Value.EventsApplied.Should().Be(1);
            getProjectionResult.Value.Name.Should().Be("Test Name");
            getProjectionResult.Value.Description.Should().Be("Test Description");
        }
    }

    [Fact]
    public async Task GivenNoEventsAreStored_ThenTheDefaultProjectionIsReturned()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var projectionId = new TestProjectionId(id);

        var result = await DomainService.GetInMemoryProjection(streamId, projectionId);

        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue();
            result.Failure.Should().BeNull();
            result.Value.Should().NotBeNull();
            result.Value.Version.Should().Be(0);
            result.Value.EventsApplied.Should().Be(0);
        }
    }

    [Fact]
    public async Task GivenNoEventsHandledByTheProjectionTypeAreStored_ThenTheDefaultProjectionIsReturned()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var projectionId = new TestProjectionId(id);

        var events = new IEvent[]
        {
            new SomethingHappenedEvent(Something: "Something")
        };
        await DomainService.SaveEvents(streamId, events, expectedEventSequence: 0);

        var result = await DomainService.GetInMemoryProjection(streamId, projectionId);

        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue();
            result.Failure.Should().BeNull();
            result.Value.Should().NotBeNull();
            result.Value.Version.Should().Be(0);
            result.Value.EventsApplied.Should().Be(0);
        }
    }

    [Fact]
    public async Task GivenProjectionEventsExist_ThenInMemoryProjectionUpToASpecificDateIsReturned()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var projectionId = new TestProjectionId(id);

        TimeProvider.SetUtcNow(new DateTime(2024, 6, 10, 12, 10, 25));
        await DomainService.SaveEvents(streamId, [
            new TestAggregateCreatedEvent(id, "Test Name", "Test Description")
        ], expectedEventSequence: 0);

        TimeProvider.SetUtcNow(new DateTime(2024, 6, 15, 17, 45, 48));
        await DomainService.SaveEvents(streamId, [
            new TestAggregateUpdatedEvent(id, "Later Name", "Later Description")
        ], expectedEventSequence: 1);

        var result = await DomainService.GetInMemoryProjection(streamId, projectionId,
            upToDate: new DateTimeOffset(new DateTime(2024, 6, 10, 12, 10, 25)));

        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.Version.Should().Be(1);
            result.Value.LatestEventSequence.Should().Be(1);
            result.Value.EventsApplied.Should().Be(1);
            result.Value.Name.Should().Be("Test Name");
            result.Value.Description.Should().Be("Test Description");
        }
    }

    [Fact]
    public async Task GivenInMemoryProjectionIsRequested_ThenTheSnapshotIsNotPersisted()
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

        await DomainService.GetInMemoryProjection(streamId, projectionId);
        var snapshotResult = await DomainService.GetProjection(streamId, projectionId);

        using (new AssertionScope())
        {
            snapshotResult.IsSuccess.Should().BeTrue();
            snapshotResult.Value.Should().BeNull();
        }
    }

    /// <summary>
    /// The three in-memory overloads narrow by <see cref="IProjectionId.EventPropertyFilter"/> too,
    /// exactly as the <c>GetInMemoryAggregate</c> overloads do. Without this, a projection sharing a
    /// stream folds another model's events whenever it is read without a snapshot.
    /// </summary>
    [Fact]
    public async Task GivenInMemoryProjectionFilteredByProperty_ThenOnlyItsOwnEventsAreApplied()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var projectionId = new TestProjectionIdWithPropertyFilter(id, "Mine");

        var events = new IEvent[]
        {
            new TestAggregateCreatedEvent(id, "Mine", "Test Description"),
            new TestAggregateUpdatedEvent(id, "Theirs", "Someone Else"),
            new TestAggregateUpdatedEvent(id, "Mine", "Updated Description")
        };
        await DomainService.SaveEvents(streamId, events, expectedEventSequence: 0);

        var result = await DomainService.GetInMemoryProjection(streamId, projectionId);

        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.EventsApplied.Should().Be(2, "the event named Theirs is not this projection's");
            result.Value.Description.Should().Be("Updated Description");
        }
    }

    [Fact]
    public async Task GivenInMemoryProjectionFilteredByPropertyUpToASequence_ThenOnlyItsOwnEventsAreApplied()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var projectionId = new TestProjectionIdWithPropertyFilter(id, "Mine");

        var events = new IEvent[]
        {
            new TestAggregateCreatedEvent(id, "Mine", "Test Description"),
            new TestAggregateUpdatedEvent(id, "Theirs", "Someone Else"),
            new TestAggregateUpdatedEvent(id, "Mine", "Updated Description")
        };
        await DomainService.SaveEvents(streamId, events, expectedEventSequence: 0);

        // The cut-off admits all three events; only the filter keeps the middle one out.
        var result = await DomainService.GetInMemoryProjection(streamId, projectionId, upToSequence: 3);

        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.EventsApplied.Should().Be(2, "the event named Theirs is not this projection's");
            result.Value.Description.Should().Be("Updated Description");
        }
    }

    [Fact]
    public async Task GivenInMemoryProjectionFilteredByPropertyUpToADate_ThenOnlyItsOwnEventsAreApplied()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var projectionId = new TestProjectionIdWithPropertyFilter(id, "Mine");

        TimeProvider.SetUtcNow(new DateTime(2024, 6, 10, 12, 10, 25));
        await DomainService.SaveEvents(streamId, [
            new TestAggregateCreatedEvent(id, "Mine", "Test Description")
        ], expectedEventSequence: 0);

        TimeProvider.SetUtcNow(new DateTime(2024, 6, 12, 9, 30, 0));
        await DomainService.SaveEvents(streamId, [
            new TestAggregateUpdatedEvent(id, "Theirs", "Someone Else"),
            new TestAggregateUpdatedEvent(id, "Mine", "Updated Description")
        ], expectedEventSequence: 1);

        // The cut-off admits all three events; only the filter keeps the middle one out.
        var result = await DomainService.GetInMemoryProjection(streamId, projectionId,
            upToDate: new DateTimeOffset(new DateTime(2024, 6, 12, 9, 30, 0)));

        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.EventsApplied.Should().Be(2, "the event named Theirs is not this projection's");
            result.Value.Description.Should().Be("Updated Description");
        }
    }
}
