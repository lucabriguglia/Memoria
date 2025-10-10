using FluentAssertions;
using FluentAssertions.Execution;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Extensions.DbContextExtensions;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Tests.Data;
using Memoria.EventSourcing.Store.Tests.Models.Aggregates;
using Memoria.EventSourcing.Store.Tests.Models.Streams;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Tests.Features.DbContext;

public class SaveAggregateTests : TestBase
{
    [Fact]
    public async Task GivenNewAggregateSaved_ThenAggregateAndEventEntitiesAreStored()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);
        var aggregate = new TestAggregate1(id, "Test Name", "Test Description");

        await using var dbContext = Shared.CreateTestDbContext();

        var saveResult = await DomainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);
        var aggregateEntity = await dbContext.Aggregates.AsNoTracking().FirstOrDefaultAsync(a => a.Id == aggregateId.ToStoreId());
        var eventEntity = await dbContext.Events.AsNoTracking().FirstOrDefaultAsync(a => a.StreamId == streamId.Id);

        using (new AssertionScope())
        {
            saveResult.IsSuccess.Should().BeTrue();

            aggregateEntity.Should().NotBeNull();
            aggregateEntity.Id.Should().Be($"{aggregateId.Id}:1");
            aggregateEntity.AggregateType.Should().Be("TestAggregate1:1");
            aggregateEntity.Version.Should().Be(1);
            aggregateEntity.LatestEventSequence.Should().Be(1);

            eventEntity.Should().NotBeNull();
            eventEntity.Id.Should().Be($"{streamId.Id}:1");
            eventEntity.EventType.Should().Be("TestAggregateCreated:1");
            eventEntity.Sequence.Should().Be(1);
        }
    }

    [Fact]
    public async Task GivenAggregateIsUpdated_ThenAggregateEntityVersionIncreasesAndAllEventEntitiesAreStored()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);
        var aggregate = new TestAggregate1(id, "Test Name", "Test Description");

        await using var dbContext = Shared.CreateTestDbContext();
        await DomainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);
        var updatedAggregateResult = await DomainService.GetAggregate(streamId, aggregateId);
        aggregate = updatedAggregateResult.Value!;
        aggregate.Update("Updated Name", "Updated Description");

        var saveResult = await DomainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 1);
        var aggregateEntity = await dbContext.Aggregates.AsNoTracking().FirstOrDefaultAsync(a => a.Id == aggregateId.ToStoreId());
        var eventEntities = await dbContext.Events.AsNoTracking().Where(a => a.StreamId == streamId.Id).ToListAsync();

        using (new AssertionScope())
        {
            saveResult.IsSuccess.Should().BeTrue();

            aggregateEntity.Should().NotBeNull();
            aggregateEntity.AggregateType.Should().Be("TestAggregate1:1");
            aggregateEntity.Version.Should().Be(2);
            aggregateEntity.LatestEventSequence.Should().Be(2);

            eventEntities.Count.Should().Be(2);
            eventEntities[0].EventType.Should().Be("TestAggregateCreated:1");
            eventEntities[0].Sequence.Should().Be(1);
            eventEntities[1].EventType.Should().Be("TestAggregateUpdated:1");
            eventEntities[1].Sequence.Should().Be(2);
        }
    }

    [Fact]
    public async Task GivenNewAggregateSaved_WhenMultipleEventsAdded_ThenAggregateAndAllEventEntitiesAreStored()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);
        var aggregate = new TestAggregate1(id, "Test Name", "Test Description");
        aggregate.Update("Updated Name", "Updated Description");

        await using var dbContext = Shared.CreateTestDbContext();
        var saveResult = await DomainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);
        var aggregateEntity = await dbContext.Aggregates.AsNoTracking().FirstOrDefaultAsync(a => a.Id == aggregateId.ToStoreId());
        var eventEntities = await dbContext.Events.AsNoTracking().Where(a => a.StreamId == streamId.Id).ToListAsync();

        using (new AssertionScope())
        {
            saveResult.IsSuccess.Should().BeTrue();

            aggregateEntity.Should().NotBeNull();
            aggregateEntity.AggregateType.Should().Be("TestAggregate1:1");
            aggregateEntity.Version.Should().Be(2);
            aggregateEntity.LatestEventSequence.Should().Be(2);

            eventEntities.Count.Should().Be(2);
            eventEntities[0].EventType.Should().Be("TestAggregateCreated:1");
            eventEntities[0].Sequence.Should().Be(1);
            eventEntities[1].EventType.Should().Be("TestAggregateUpdated:1");
            eventEntities[1].Sequence.Should().Be(2);
        }
    }

    [Fact]
    public async Task GivenStoredEventForAnAggregateIsAppliedInAnotherAggregate_ThenTheOtherAggregateEntityIsStored()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);

        var testAggregate1Key = new TestAggregate1Id(id);
        var testAggregate2Key = new TestAggregate2Id(id);
        var testAggregate1 = new TestAggregate1(id, "Test Name", "Test Description");

        await using var dbContext = Shared.CreateTestDbContext();

        var trackResult = await dbContext.TrackAggregate(streamId, testAggregate1Key, testAggregate1, expectedEventSequence: 0);
        await dbContext.TrackEventEntities(streamId, testAggregate2Key, trackResult.Value.EventEntities!, expectedEventSequence: 0);
        await dbContext.Save();

        var eventEntity = await dbContext.Events.AsNoTracking().FirstOrDefaultAsync(a => a.StreamId == streamId.Id);
        var aggregate1Entity = await dbContext.Aggregates.AsNoTracking().FirstOrDefaultAsync(a => a.Id == testAggregate1Key.ToStoreId());
        var aggregate2Entity = await dbContext.Aggregates.AsNoTracking().FirstOrDefaultAsync(a => a.Id == testAggregate2Key.ToStoreId());

        using (new AssertionScope())
        {
            eventEntity.Should().NotBeNull();

            aggregate1Entity.Should().NotBeNull();
            aggregate1Entity.LatestEventSequence.Should().Be(1);

            aggregate2Entity.Should().NotBeNull();
            aggregate2Entity.LatestEventSequence.Should().Be(1);
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

        var timeProvider = new FakeTimeProvider();
        timeProvider.SetUtcNow(now);
        var dbContext = new TestDbContext(Shared.CreateContextOptions(), timeProvider, Shared.CreateHttpContextAccessor());
        var domainService = Shared.CreateDomainService(dbContext);

        await domainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);
        var aggregateEntity = await dbContext.Aggregates.AsNoTracking().FirstOrDefaultAsync(a => a.Id == aggregateId.ToStoreId());
        var eventEntity = await dbContext.Events.AsNoTracking().FirstOrDefaultAsync(a => a.StreamId == streamId.Id);

        using (new AssertionScope())
        {
            aggregateEntity.Should().NotBeNull();
            aggregateEntity.CreatedBy.Should().Be("TestUser");
            aggregateEntity.CreatedDate.Should().Be(now);
            aggregateEntity.UpdatedBy.Should().Be("TestUser");
            aggregateEntity.UpdatedDate.Should().Be(now);

            eventEntity.Should().NotBeNull();
            eventEntity.CreatedBy.Should().Be("TestUser");
            eventEntity.CreatedDate.Should().Be(now);
        }
    }

    [Fact]
    public async Task GivenAggregateUpdated_ThenAuditablePropertiesArePopulated()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);
        var aggregate = new TestAggregate1(id, "Test Name", "Test Description");

        var timeProvider = new FakeTimeProvider();

        var createDate = new DateTime(2024, 6, 10, 12, 0, 0, DateTimeKind.Utc);
        timeProvider.SetUtcNow(createDate);
        using (var domainService = Shared.CreateDomainService(timeProvider, Shared.CreateHttpContextAccessor()))
        {
            await domainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);
        }

        var updateDate = new DateTime(2024, 6, 15, 18, 0, 0, DateTimeKind.Utc);
        timeProvider.SetUtcNow(updateDate);
        var dbContext = new TestDbContext(Shared.CreateContextOptions(), timeProvider, Shared.CreateHttpContextAccessor());
        using (var domainService = Shared.CreateDomainService(dbContext))
        {
            var aggregateToUpdateResult = await domainService.GetAggregate(streamId, aggregateId);
            aggregateToUpdateResult.Value!.Update("Updated Name", "Updated Description");
            await domainService.SaveAggregate(streamId, aggregateId, aggregateToUpdateResult.Value, expectedEventSequence: 1);

            var aggregateEntity = await dbContext.Aggregates.AsNoTracking().FirstOrDefaultAsync(a => a.Id == aggregateId.ToStoreId());
            var eventEntity = await dbContext.Events.AsNoTracking().FirstOrDefaultAsync(a => a.StreamId == streamId.Id);

            using (new AssertionScope())
            {
                aggregateEntity.Should().NotBeNull();
                aggregateEntity.CreatedBy.Should().Be("TestUser");
                aggregateEntity.CreatedDate.Should().Be(createDate);
                aggregateEntity.UpdatedBy.Should().Be("TestUser");
                aggregateEntity.UpdatedDate.Should().Be(updateDate);

                eventEntity.Should().NotBeNull();
                eventEntity.CreatedBy.Should().Be("TestUser");
                eventEntity.CreatedDate.Should().Be(createDate);
            }
        }
    }
}
