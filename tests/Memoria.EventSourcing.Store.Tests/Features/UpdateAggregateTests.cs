using FluentAssertions;
using FluentAssertions.Execution;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.Tests.Models.Aggregates;
using Memoria.EventSourcing.Store.Tests.Models.Events;
using Memoria.EventSourcing.Store.Tests.Models.Streams;
using Xunit;

namespace Memoria.EventSourcing.Store.Tests.Features;

public abstract class UpdateAggregateTests(IDomainServiceFactory domainServiceFactory) : TestBase(domainServiceFactory)
{
    [Fact]
    public async Task GivenEventsHandledByTheAggregateAreStoredSeparately_WhenAggregateIsUpdated_ThenAggregateVersionIsIncreasedAndTheUpdatedAggregateIsReturned()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);
        var aggregate = new TestAggregate1(id, "Test Name", "Test Description");

        await DomainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);
        await DomainService.SaveEvents(streamId, [new TestAggregateUpdatedEvent(id, "Updated Name", "Updated Description")], expectedEventSequence: 1);
        var updatedAggregateResult = await DomainService.UpdateAggregate(streamId, aggregateId);

        using (new AssertionScope())
        {
            updatedAggregateResult.IsSuccess.Should().BeTrue();

            updatedAggregateResult.Value.Should().NotBeNull();

            updatedAggregateResult.Value.StreamId.Should().Be(streamId.Id);
            updatedAggregateResult.Value.AggregateId.Should().Be(aggregateId.ToStoreId());
            updatedAggregateResult.Value.Version.Should().Be(2);

            updatedAggregateResult.Value.Id.Should().Be(id);
            updatedAggregateResult.Value.Name.Should().Be("Updated Name");
            updatedAggregateResult.Value.Description.Should().Be("Updated Description");
        }
    }

    [Fact]
    public async Task GivenEventsHandledByTheAggregateAreStoredSeparately_WhenAggregateDoesNotExist_ThenAggregateIsStoredAndReturned()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);

        var events = new List<IEvent>
        {
            new TestAggregateCreatedEvent(id, "Test Name", "Test Description"),
            new TestAggregateUpdatedEvent(id, "Updated Name", "Updated Description")
        };
        await DomainService.SaveEvents(streamId, events.ToArray(), expectedEventSequence: 0);
        var updatedAggregateResult = await DomainService.UpdateAggregate(streamId, aggregateId);

        using (new AssertionScope())
        {
            updatedAggregateResult.IsSuccess.Should().BeTrue();

            updatedAggregateResult.Value.Should().NotBeNull();

            updatedAggregateResult.Value.StreamId.Should().Be(streamId.Id);
            updatedAggregateResult.Value.AggregateId.Should().Be(aggregateId.ToStoreId());
            updatedAggregateResult.Value.Version.Should().Be(2);

            updatedAggregateResult.Value.Id.Should().Be(id);
            updatedAggregateResult.Value.Name.Should().Be("Updated Name");
            updatedAggregateResult.Value.Description.Should().Be("Updated Description");
        }
    }
}

