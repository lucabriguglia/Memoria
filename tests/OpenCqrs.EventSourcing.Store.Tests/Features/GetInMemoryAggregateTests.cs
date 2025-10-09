using FluentAssertions;
using FluentAssertions.Execution;
using OpenCqrs.EventSourcing.Domain;
using OpenCqrs.EventSourcing.Store.Tests.Models.Aggregates;
using OpenCqrs.EventSourcing.Store.Tests.Models.Events;
using OpenCqrs.EventSourcing.Store.Tests.Models.Streams;
using Xunit;

namespace OpenCqrs.EventSourcing.Store.Tests.Features;

public abstract class GetInMemoryAggregateTests(IDomainServiceFactory domainServiceFactory) : TestBase(domainServiceFactory)
{
    [Fact]
    public async Task GivenAggregateDoesExist_ThenTheInMemoryAggregateIsReturned()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);
        var aggregate = new TestAggregate1(id, "Test Name", "Test Description");

        await DomainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);

        var getAggregateResult = await DomainService.GetInMemoryAggregate(streamId, aggregateId);

        using (new AssertionScope())
        {
            getAggregateResult.IsSuccess.Should().BeTrue();

            getAggregateResult.Value.Should().NotBeNull();

            getAggregateResult.Value.StreamId.Should().Be(streamId.Id);
            getAggregateResult.Value.AggregateId.Should().Be(aggregateId.ToStoreId());
            getAggregateResult.Value.Version.Should().Be(1);

            getAggregateResult.Value.Id.Should().Be(aggregate.Id);
            getAggregateResult.Value.Name.Should().Be(aggregate.Name);
            getAggregateResult.Value.Description.Should().Be(aggregate.Description);
        }
    }

    [Fact]
    public async Task GivenAggregateDoesExist_ThenTheInMemoryAggregateUpToSequenceIsReturned()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);
        var aggregate = new TestAggregate1(id, "Test Name", "Test Description");

        await DomainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);

        var events = new IEvent[]
        {
            new TestAggregateUpdatedEvent(id, "Updated Name", "Updated Description")
        };
        await DomainService.SaveEvents(streamId, events, expectedEventSequence: 1);

        var getAggregateResult = await DomainService.GetInMemoryAggregate(streamId, aggregateId, upToSequence: 1);

        using (new AssertionScope())
        {
            getAggregateResult.IsSuccess.Should().BeTrue();

            getAggregateResult.Value.Should().NotBeNull();

            getAggregateResult.Value.StreamId.Should().Be(streamId.Id);
            getAggregateResult.Value.AggregateId.Should().Be(aggregateId.ToStoreId());
            getAggregateResult.Value.Version.Should().Be(1);

            getAggregateResult.Value.Id.Should().Be(aggregate.Id);
            getAggregateResult.Value.Name.Should().Be(aggregate.Name);
            getAggregateResult.Value.Description.Should().Be(aggregate.Description);
        }
    }

    [Fact]
    public async Task GivenAggregateDoesNotExist_WhenEventsHandledByTheAggregateTypeAreStored_ThenTheInMemoryAggregateIsReturned()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);

        var events = new IEvent[]
        {
            new TestAggregateCreatedEvent(id, "Test Name", "Test Description"),
            new TestAggregateUpdatedEvent(id, "Updated Name", "Updated Description")
        };
        await DomainService.SaveEvents(streamId, events, expectedEventSequence: 0);

        var aggregateResult = await DomainService.GetInMemoryAggregate(streamId, aggregateId);

        using (new AssertionScope())
        {
            aggregateResult.IsSuccess.Should().BeTrue();

            aggregateResult.Value.Should().NotBeNull();
            aggregateResult.Value.StreamId.Should().Be(streamId.Id);
            aggregateResult.Value.AggregateId.Should().Be(aggregateId.ToStoreId());
            aggregateResult.Value.Version.Should().Be(2);
            aggregateResult.Value.Id.Should().Be(id);
            aggregateResult.Value.Name.Should().Be("Updated Name");
            aggregateResult.Value.Description.Should().Be("Updated Description");
        }
    }

    [Fact]
    public async Task GivenAggregateDoesNotExist_WhenEventsHandledByTheAggregateTypeAreStored_ThenTheInMemoryAggregateUpToSequenceIsReturned()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);

        var events = new IEvent[]
        {
            new TestAggregateCreatedEvent(id, "Test Name", "Test Description"),
            new TestAggregateUpdatedEvent(id, "Updated Name", "Updated Description")
        };
        await DomainService.SaveEvents(streamId, events, expectedEventSequence: 0);

        var aggregateResult = await DomainService.GetInMemoryAggregate(streamId, aggregateId, upToSequence: 1);

        using (new AssertionScope())
        {
            aggregateResult.IsSuccess.Should().BeTrue();

            aggregateResult.Value.Should().NotBeNull();
            aggregateResult.Value.StreamId.Should().Be(streamId.Id);
            aggregateResult.Value.AggregateId.Should().Be(aggregateId.ToStoreId());
            aggregateResult.Value.Version.Should().Be(1);
            aggregateResult.Value.Id.Should().Be(id);
            aggregateResult.Value.Name.Should().Be("Test Name");
            aggregateResult.Value.Description.Should().Be("Test Description");
        }
    }

    [Fact]
    public async Task GivenAggregateDoesNotExist_WhenNoEventsAreStored_TheDefaultAggregateIsReturned()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);

        var result = await DomainService.GetInMemoryAggregate(streamId, aggregateId);

        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue();
            result.Failure.Should().BeNull();
            result.Value.Should().NotBeNull();
            result.Value.Version.Should().Be(0);
        }
    }

    [Fact]
    public async Task GivenAggregateDoesNotExist_WhenNoEventsHandledByTheAggregateTypeAreStored_TheDefaultAggregateIsReturned()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);

        var events = new IEvent[]
        {
            new SomethingHappenedEvent(Something: "Something")
        };
        await DomainService.SaveEvents(streamId, events, expectedEventSequence: 1);

        var result = await DomainService.GetInMemoryAggregate(streamId, aggregateId);

        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue();
            result.Failure.Should().BeNull();
            result.Value.Should().NotBeNull();
            result.Value.Version.Should().Be(0);
        }
    }

    [Fact]
    public async Task GivenAggregateExists_ThenInMemoryAggregateUpToASpecificDateIsReturned()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);

        TimeProvider.SetUtcNow(new DateTime(2024, 6, 10, 12, 10, 25));
        await DomainService.SaveEvents(streamId, [
            new TestAggregateCreatedEvent(id, "Test Name", "Test Description"),
            new SomethingHappenedEvent("Something2")
        ], expectedEventSequence: 0);

        TimeProvider.SetUtcNow(new DateTime(2024, 6, 15, 17, 45, 48));
        await DomainService.SaveEvents(streamId, [
            new SomethingHappenedEvent("Something3"),
            new SomethingHappenedEvent("Something4")
        ], expectedEventSequence: 2);

        TimeProvider.SetUtcNow(new DateTime(2024, 6, 15, 17, 45, 49));
        await DomainService.SaveEvents(streamId, [
            new SomethingHappenedEvent("Something5"),
            new SomethingHappenedEvent("Something6")
        ], expectedEventSequence: 4);

        var result = await DomainService.GetInMemoryAggregate(streamId, aggregateId,
            upToDate: new DateTimeOffset(new DateTime(2024, 6, 15, 17, 45, 48)));
        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.Version.Should().Be(1);
        }
    }
}
