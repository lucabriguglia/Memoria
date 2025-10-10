using System.Diagnostics;
using FluentAssertions;
using FluentAssertions.Execution;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.Tests.Models.Aggregates;
using Memoria.EventSourcing.Store.Tests.Models.Events;
using Memoria.EventSourcing.Store.Tests.Models.Streams;
using Memoria.Results;
using Xunit;

namespace Memoria.EventSourcing.Store.Tests.Features;

public abstract class SaveAggregateTests(IDomainServiceFactory domainServiceFactory) : TestBase(domainServiceFactory)
{
    [Fact]
    public async Task GivenAnotherEventWithTheExpectedSequenceIsAlreadyStored_ThenReturnsConcurrencyExceptionFailure()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);
        var aggregate = new TestAggregate1(id, "Test Name", "Test Description");

        await DomainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);
        var saveResult = await DomainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);

        using (new AssertionScope())
        {
            saveResult.IsSuccess.Should().BeFalse();
            saveResult.Failure.Should().NotBeNull();
            saveResult.Failure.ErrorCode.Should().Be(ErrorCode.Error);
            saveResult.Failure.Title.Should().Be("Error");
            saveResult.Failure.Description.Should().Be("There was an error when processing the request");

            var activityEvent = Activity.Current?.Events.SingleOrDefault(e => e.Name == "Concurrency Exception");
            activityEvent.Should().NotBeNull();
            activityEvent.Value.Tags.First().Key.Should().Be("streamId");
            activityEvent.Value.Tags.First().Value.Should().Be(streamId.Id);
            activityEvent.Value.Tags.Skip(1).First().Key.Should().Be("expectedEventSequence");
            activityEvent.Value.Tags.Skip(1).First().Value.Should().Be(0);
            activityEvent.Value.Tags.Skip(2).First().Key.Should().Be("latestEventSequence");
            activityEvent.Value.Tags.Skip(2).First().Value.Should().Be(1);
        }
    }

    [Fact]
    public async Task GivenEventsNotHandledByTheAggregateStored_WhenAggregateIsUpdated_ThenLastEventSequenceIsGreaterThenAggregateVersion()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate2Id(id);
        var aggregate = new TestAggregate2(id, "Test Name", "Test Description");

        await DomainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);

        var events = new IEvent[]
        {
            new SomethingHappenedEvent("Something1"),
            new SomethingHappenedEvent("Something2"),
            new SomethingHappenedEvent("Something3"),
            new SomethingHappenedEvent("Something4")
        };
        await DomainService.SaveEvents(streamId, events, expectedEventSequence: 1);

        var aggregateToUpdateResult = await DomainService.GetAggregate(streamId, aggregateId);
        aggregateToUpdateResult.Value!.Update("Updated Name", "Updated Description");
        await DomainService.SaveAggregate(streamId, aggregateId, aggregateToUpdateResult.Value, expectedEventSequence: 5);

        var aggregateResult = await DomainService.GetAggregate(streamId, aggregateId);

        using (new AssertionScope())
        {
            aggregateResult.IsSuccess.Should().BeTrue();

            aggregateResult.Value.Should().NotBeNull();

            aggregateResult.Value.StreamId.Should().Be(streamId.Id);
            aggregateResult.Value.AggregateId.Should().Be(aggregateId.ToStoreId());
            aggregateResult.Value.Version.Should().Be(2);
            aggregateResult.Value.LatestEventSequence.Should().Be(6);

            aggregateResult.Value.Id.Should().Be(id);
            aggregateResult.Value.Name.Should().Be("Updated Name");
            aggregateResult.Value.Description.Should().Be("Updated Description");
        }
    }
}
