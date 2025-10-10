using FluentAssertions;
using FluentAssertions.Execution;
using Memoria.EventSourcing;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Extensions;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Extensions.DbContextExtensions;
using Microsoft.Extensions.Time.Testing;
using OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Tests.Data;
using OpenCqrs.EventSourcing.Store.Tests.Models.Aggregates;
using OpenCqrs.EventSourcing.Store.Tests.Models.Events;
using OpenCqrs.EventSourcing.Store.Tests.Models.Streams;
using Xunit;

namespace OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Tests.Features.DbContext;

public class GetAggregateEventsTests : TestBase
{
    [Fact]
    public async Task GivenAggregateSaved_ThenOnlyAggregateEventsAppliedAreReturned()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate2Id(id);
        var aggregate = new TestAggregate2(id, "Test Name", "Test Description");

        var timeProvider = new FakeTimeProvider();

        var appliedDate1 = new DateTime(2024, 6, 10, 12, 0, 0, DateTimeKind.Utc);
        timeProvider.SetUtcNow(appliedDate1);
        await using (var dbContext = new TestDbContext(Shared.CreateContextOptions(), timeProvider, Shared.CreateHttpContextAccessor()))
        {
            await dbContext.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);

            dbContext.Add(new SomethingHappenedEvent("Something1").ToEventEntity(streamId, sequence: 2));
            dbContext.Add(new SomethingHappenedEvent("Something2").ToEventEntity(streamId, sequence: 3));
            dbContext.Add(new SomethingHappenedEvent("Something3").ToEventEntity(streamId, sequence: 4));
            dbContext.Add(new SomethingHappenedEvent("Something4").ToEventEntity(streamId, sequence: 5));

            await dbContext.SaveChangesAsync();
        }

        var appliedDate2 = new DateTime(2024, 6, 10, 13, 0, 0, DateTimeKind.Utc);
        timeProvider.SetUtcNow(appliedDate2);
        await using (var dbContext = new TestDbContext(Shared.CreateContextOptions(), timeProvider, Shared.CreateHttpContextAccessor()))
        {
            var aggregateToUpdateResult = await dbContext.GetAggregate(streamId, aggregateId);
            aggregateToUpdateResult.Value!.Update("Updated Name", "Updated Description");

            await dbContext.SaveAggregate(streamId, aggregateId, aggregateToUpdateResult.Value, expectedEventSequence: 5);

            var result = await dbContext.GetAggregateEventEntities(aggregateId);

            using (new AssertionScope())
            {
                result.IsSuccess.Should().BeTrue();
                result.Value.Should().NotBeNull();
                result.Value.Count.Should().Be(2);
                result.Value.First().AppliedDate.Should().Be(appliedDate1);
                result.Value.Last().AppliedDate.Should().Be(appliedDate2);
            }
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

        var timeProvider = new FakeTimeProvider();

        await using var dbContext = new TestDbContext(Shared.CreateContextOptions(), timeProvider, Shared.CreateHttpContextAccessor());

        var appliedDate = new DateTime(2024, 6, 10, 12, 0, 0, DateTimeKind.Utc);
        timeProvider.SetUtcNow(appliedDate);
        var trackResult = await dbContext.TrackAggregate(streamId, testAggregate1Key, testAggregate1, expectedEventSequence: 0);
        await dbContext.TrackEventEntities(streamId, testAggregate2Key, trackResult.Value.EventEntities!, expectedEventSequence: 0);
        await dbContext.Save();

        var result = await dbContext.GetAggregateEventEntities(testAggregate2Key);

        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.Count.Should().Be(1);
            result.Value.First().AppliedDate.Should().Be(appliedDate);
        }
    }

    [Fact]
    public async Task GivenAggregateDoesNotExist_WhenEventsAreStoredAndAppliedWhenGettingTheAggregate_ThenAggregateEventsAppliedAreReturned()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);

        var timeProvider = new FakeTimeProvider();

        var date1 = new DateTime(2024, 6, 10, 12, 0, 0, DateTimeKind.Utc);
        timeProvider.SetUtcNow(date1);
        await using (var dbContext = new TestDbContext(Shared.CreateContextOptions(), timeProvider, Shared.CreateHttpContextAccessor()))
        {
            dbContext.Add(new TestAggregateCreatedEvent(id, "Test Name", "Test Description").ToEventEntity(streamId, sequence: 1));
            dbContext.Add(new TestAggregateUpdatedEvent(id, "Updated Name", "Updated Description").ToEventEntity(streamId, sequence: 2));
            await dbContext.SaveChangesAsync();
        }

        var date2 = new DateTime(2024, 6, 10, 13, 0, 0, DateTimeKind.Utc);
        timeProvider.SetUtcNow(date2);
        await using (var dbContext = new TestDbContext(Shared.CreateContextOptions(), timeProvider, Shared.CreateHttpContextAccessor()))
        {
            await dbContext.GetAggregate(streamId, aggregateId, ReadMode.SnapshotOrCreate);

            var result = await dbContext.GetAggregateEventEntities(aggregateId);

            using (new AssertionScope())
            {
                result.IsSuccess.Should().BeTrue();
                result.Value.Should().NotBeNull();
                result.Value.Count.Should().Be(2);
                result.Value.First().AppliedDate.Should().Be(date2);
                result.Value.Last().AppliedDate.Should().Be(date2);
            }
        }
    }

    [Fact]
    public async Task GivenEventsHandledByTheAggregateAreStoredSeparately_WhenApplyNewEventsIsRequestedWhenGettingTheAggregate_ThenAggregateEventsAppliedAreReturned()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);
        var aggregate = new TestAggregate1(id, "Test Name", "Test Description");

        var timeProvider = new FakeTimeProvider();

        var date1 = new DateTime(2024, 6, 10, 12, 0, 0, DateTimeKind.Utc);
        timeProvider.SetUtcNow(date1);
        await using (var dbContext = new TestDbContext(Shared.CreateContextOptions(), timeProvider, Shared.CreateHttpContextAccessor()))
        {
            await dbContext.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);
        }

        var date2 = new DateTime(2024, 6, 10, 13, 0, 0, DateTimeKind.Utc);
        timeProvider.SetUtcNow(date2);
        await using (var dbContext = new TestDbContext(Shared.CreateContextOptions(), timeProvider, Shared.CreateHttpContextAccessor()))
        {
            dbContext.Add(new TestAggregateUpdatedEvent(id, "Updated Name", "Updated Description").ToEventEntity(streamId, sequence: 2));
            await dbContext.Save();
        }

        var date3 = new DateTime(2024, 6, 10, 14, 0, 0, DateTimeKind.Utc);
        timeProvider.SetUtcNow(date3);
        await using (var dbContext = new TestDbContext(Shared.CreateContextOptions(), timeProvider, Shared.CreateHttpContextAccessor()))
        {
            await dbContext.GetAggregate(streamId, aggregateId, ReadMode.SnapshotWithNewEvents);
            var result = await dbContext.GetAggregateEventEntities(aggregateId);

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

    [Fact]
    public async Task GivenEventsHandledByTheAggregateAreStoredSeparately_WhenAggregateIsUpdated_ThenAggregateEventsAppliedAreReturned()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);
        var aggregate = new TestAggregate1(id, "Test Name", "Test Description");

        var timeProvider = new FakeTimeProvider();

        var date1 = new DateTime(2024, 6, 10, 12, 0, 0, DateTimeKind.Utc);
        timeProvider.SetUtcNow(date1);
        await using (var dbContext = new TestDbContext(Shared.CreateContextOptions(), timeProvider, Shared.CreateHttpContextAccessor()))
        {
            await dbContext.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);
        }

        var date2 = new DateTime(2024, 6, 10, 13, 0, 0, DateTimeKind.Utc);
        timeProvider.SetUtcNow(date2);
        await using (var dbContext = new TestDbContext(Shared.CreateContextOptions(), timeProvider, Shared.CreateHttpContextAccessor()))
        {
            dbContext.Add(new TestAggregateUpdatedEvent(id, "Updated Name", "Updated Description").ToEventEntity(streamId, sequence: 2));
            await dbContext.Save();
        }

        var date3 = new DateTime(2024, 6, 10, 14, 0, 0, DateTimeKind.Utc);
        timeProvider.SetUtcNow(date3);
        await using (var dbContext = new TestDbContext(Shared.CreateContextOptions(), timeProvider, Shared.CreateHttpContextAccessor()))
        {
            await dbContext.UpdateAggregate(streamId, aggregateId);
            var result = await dbContext.GetAggregateEventEntities(aggregateId);

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
}
