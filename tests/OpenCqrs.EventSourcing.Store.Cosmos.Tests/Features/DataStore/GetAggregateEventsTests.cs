using FluentAssertions;
using FluentAssertions.Execution;
using OpenCqrs.EventSourcing.Domain;
using OpenCqrs.EventSourcing.Store.Tests.Models.Aggregates;
using OpenCqrs.EventSourcing.Store.Tests.Models.Events;
using OpenCqrs.EventSourcing.Store.Tests.Models.Streams;
using Xunit;

namespace OpenCqrs.EventSourcing.Store.Cosmos.Tests.Features.DataStore;

public class GetAggregateEventsTests : TestBase
{
    [Fact]
    public async Task GivenAggregateSaved_ThenOnlyAggregateEventsAppliedAreReturned()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate2Id(id);
        var aggregate = new TestAggregate2(id, "Test Name", "Test Description");

        var appliedDate1 = new DateTime(2024, 6, 10, 12, 0, 0, DateTimeKind.Utc);
        TimeProvider.SetUtcNow(appliedDate1);
        await DomainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);
        var events = new IEvent[]
        {
            new SomethingHappenedEvent("Something1"),
            new SomethingHappenedEvent("Something2"),
            new SomethingHappenedEvent("Something3"),
            new SomethingHappenedEvent("Something4")
        };
        await DomainService.SaveEvents(streamId, events, expectedEventSequence: 1);

        var appliedDate2 = new DateTime(2024, 6, 10, 13, 0, 0, DateTimeKind.Utc);
        TimeProvider.SetUtcNow(appliedDate2);
        var aggregateToUpdateResult = await DomainService.GetAggregate(streamId, aggregateId);
        aggregateToUpdateResult.Value!.Update("Updated Name", "Updated Description");
        await DomainService.SaveAggregate(streamId, aggregateId, aggregateToUpdateResult.Value, expectedEventSequence: 5);

        var result = await DataStore.GetAggregateEventDocuments(streamId, aggregateId);

        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.Count.Should().Be(2);
            result.Value.First().Id.Should().Be($"{aggregateId.Id}:1|{streamId.Id}:1");
            result.Value.First().AppliedDate.Should().Be(appliedDate1);
            result.Value.Last().Id.Should().Be($"{aggregateId.Id}:1|{streamId.Id}:6");
            result.Value.Last().AppliedDate.Should().Be(appliedDate2);
        }
    }

    [Fact]
    public async Task GivenStoredEventForAnAggregateIsAppliedInAnotherAggregate_ThenOnlyAggregateEventsAppliedAreReturned()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);

        var testAggregate1Key = new TestAggregate1Id(id);
        var testAggregate2Key = new TestAggregate2Id(id);
        var testAggregate1 = new TestAggregate1(id, "Test Name", "Test Description");

        var appliedDate = new DateTime(2024, 6, 10, 12, 0, 0, DateTimeKind.Utc);
        TimeProvider.SetUtcNow(appliedDate);
        await DomainService.SaveAggregate(streamId, testAggregate1Key, testAggregate1, expectedEventSequence: 0);
        await DomainService.GetAggregate(streamId, testAggregate2Key, ReadMode.SnapshotOrCreate);
        var result = await DataStore.GetAggregateEventDocuments(streamId, testAggregate2Key);

        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.Count.Should().Be(1);
            result.Value.First().Id.Should().Be($"{testAggregate2Key.Id}:1|{streamId.Id}:1");
            result.Value.First().AppliedDate.Should().Be(appliedDate);
        }
    }

    [Fact]
    public async Task GivenAggregateDoesNotExist_WhenEventsAreStoredAndAppliedWhenGettingTheAggregate_ThenAggregateEventsAppliedAreReturned()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);

        var date1 = new DateTime(2024, 6, 10, 12, 0, 0, DateTimeKind.Utc);
        TimeProvider.SetUtcNow(date1);
        var events = new IEvent[]
        {
            new TestAggregateCreatedEvent(id, "Test Name", "Test Description"),
            new TestAggregateUpdatedEvent(id, "Updated Name", "Updated Description")
        };
        await DomainService.SaveEvents(streamId, events, expectedEventSequence: 0);

        var date2 = new DateTime(2024, 6, 10, 13, 0, 0, DateTimeKind.Utc);
        TimeProvider.SetUtcNow(date2);
        await DomainService.GetAggregate(streamId, aggregateId, ReadMode.SnapshotOrCreate);

        var result = await DataStore.GetAggregateEventDocuments(streamId, aggregateId);

        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.Count.Should().Be(2);
            result.Value.First().AppliedDate.Should().Be(date2);
            result.Value.Last().AppliedDate.Should().Be(date2);
        }
    }

    [Fact]
    public async Task GivenEventsHandledByTheAggregateAreStoredSeparately_WhenApplyNewEventsIsRequestedWhenGettingTheAggregate_ThenAggregateEventsAppliedAreReturned()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);
        var aggregate = new TestAggregate1(id, "Test Name", "Test Description");

        var date1 = new DateTime(2024, 6, 10, 12, 0, 0, DateTimeKind.Utc);
        TimeProvider.SetUtcNow(date1);
        await DomainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);

        var date2 = new DateTime(2024, 6, 10, 13, 0, 0, DateTimeKind.Utc);
        TimeProvider.SetUtcNow(date2);
        var events = new IEvent[]
        {
            new TestAggregateUpdatedEvent(id, "Updated Name", "Updated Description")
        };
        await DomainService.SaveEvents(streamId, events, expectedEventSequence: 1);

        var date3 = new DateTime(2024, 6, 10, 14, 0, 0, DateTimeKind.Utc);
        TimeProvider.SetUtcNow(date3);
        await DomainService.GetAggregate(streamId, aggregateId, ReadMode.SnapshotWithNewEvents);
        var result = await DataStore.GetAggregateEventDocuments(streamId, aggregateId);

        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.Count.Should().Be(2);
            result.Value.First().AppliedDate.Should().Be(date1);
            result.Value.Last().AppliedDate.Should().Be(date3);
        }
    }

    [Fact]
    public async Task GivenEventsHandledByTheAggregateAreStoredSeparately_WhenAggregateIsUpdated_ThenAggregateEventsAppliedAreReturned()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);
        var aggregate = new TestAggregate1(id, "Test Name", "Test Description");

        var date1 = new DateTime(2024, 6, 10, 12, 0, 0, DateTimeKind.Utc);
        TimeProvider.SetUtcNow(date1);
        await DomainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);

        var date2 = new DateTime(2024, 6, 10, 13, 0, 0, DateTimeKind.Utc);
        TimeProvider.SetUtcNow(date2);
        var events = new IEvent[]
        {
            new TestAggregateUpdatedEvent(id, "Updated Name", "Updated Description")
        };
        await DomainService.SaveEvents(streamId, events, expectedEventSequence: 1);

        var date3 = new DateTime(2024, 6, 10, 14, 0, 0, DateTimeKind.Utc);
        TimeProvider.SetUtcNow(date3);
        await DomainService.UpdateAggregate(streamId, aggregateId);
        var result = await DataStore.GetAggregateEventDocuments(streamId, aggregateId);

        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.Count.Should().Be(2);
            result.Value.First().AppliedDate.Should().Be(date1);
            result.Value.Last().AppliedDate.Should().Be(date3);
        }
    }
}
