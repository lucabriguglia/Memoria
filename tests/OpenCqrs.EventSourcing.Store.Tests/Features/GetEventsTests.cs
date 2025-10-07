using FluentAssertions;
using FluentAssertions.Execution;
using OpenCqrs.EventSourcing.Store.Tests.Models.Aggregates;
using OpenCqrs.EventSourcing.Store.Tests.Models.Events;
using OpenCqrs.EventSourcing.Store.Tests.Models.Streams;
using Xunit;

namespace OpenCqrs.EventSourcing.Store.Tests.Features;

public class GetEventsTests(IDomainServiceFactory domainServiceFactory) : TestBase(domainServiceFactory)
{
    [Fact]
    public async Task GiveMultipleEventsSaved_WhenAllEventsAreRequested_ThenAllEventsAreReturned()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);
        var aggregate = new TestAggregate1(id, "Test Name", "Test Description");
        aggregate.Update("Updated Name", "Updated Description");
        aggregate.Update("Updated Name 2", "Updated Description 2");
        aggregate.Update("Updated Name 3", "Updated Description 3");

        await DomainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);
        var events = await DomainService.GetEvents(streamId);

        events.Value!.Count.Should().Be(4);
    }

    [Fact]
    public async Task GiveMultipleEventsSaved_WhenOnlyEventsUpToASpecificSequenceAreRequested_ThenOnlyEventsUpToTheSpecificSequenceAreReturned()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);
        var aggregate = new TestAggregate1(id, "Test Name", "Test Description");
        aggregate.Update("Updated Name", "Updated Description");
        aggregate.Update("Updated Name 2", "Updated Description 2");
        aggregate.Update("Updated Name 3", "Updated Description 3");

        await DomainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);
        var events = await DomainService.GetEventsUpToSequence(streamId, upToSequence: 3);

        events.Value!.Count.Should().Be(3);
    }

    [Fact]
    public async Task GiveMultipleEventsSaved_WhenOnlyEventsFromASpecificSequenceAreRequested_ThenOnlyEventsFromTheSpecificSequenceAreReturned()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);
        var aggregate = new TestAggregate1(id, "Test Name", "Test Description");
        aggregate.Update("Updated Name", "Updated Description");
        aggregate.Update("Updated Name 2", "Updated Description 2");
        aggregate.Update("Updated Name 3", "Updated Description 3");

        await DomainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);
        var events = await DomainService.GetEventsFromSequence(streamId, fromSequence: 3);

        events.Value!.Count.Should().Be(2);
    }

    [Fact]
    public async Task GiveMultipleEventsStored_WhenOnlyEventsBetweenSpecificSequencesAreRequested_ThenOnlyEventsBetweenSpecificSequencesAreReturned()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);
        var aggregate = new TestAggregate1(id, "Test Name", "Test Description");
        aggregate.Update("Updated Name 1", "Updated Description 1");
        aggregate.Update("Updated Name 2", "Updated Description 2");
        aggregate.Update("Updated Name 3", "Updated Description 3");
        aggregate.Update("Updated Name 4", "Updated Description 4");

        await DomainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);
        var events = await DomainService.GetEventsBetweenSequences(streamId, fromSequence: 2, toSequence: 4);

        events.Value!.Count.Should().Be(3);
    }

    [Fact]
    public async Task GiveMultipleEventsStored_WhenOnlyEventsUpToASpecificDateAreRequested_ThenEventsUpToASpecificDateAreReturned()
    {
        var streamId = new TestStreamId(Guid.NewGuid().ToString());

        TimeProvider.SetUtcNow(new DateTime(2024, 6, 10, 12, 10, 25));
        await DomainService.SaveEvents(streamId, [
            new SomethingHappenedEvent("Something1"),
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

        var result = await DomainService.GetEventsUpToDate(streamId, upToDate: new DateTimeOffset(new DateTime(2024, 6, 15, 17, 45, 48)));
        using (new AssertionScope())
        {
            result.Value!.Count.Should().Be(4);
            result.Value[0].Should().BeOfType<SomethingHappenedEvent>().Which.Something.Should().Be("Something1");
            result.Value[1].Should().BeOfType<SomethingHappenedEvent>().Which.Something.Should().Be("Something2");
            result.Value[2].Should().BeOfType<SomethingHappenedEvent>().Which.Something.Should().Be("Something3");
            result.Value[3].Should().BeOfType<SomethingHappenedEvent>().Which.Something.Should().Be("Something4");
        }
    }

    [Fact]
    public async Task GiveMultipleEventsStored_WhenOnlyEventsUpToASpecificDateFilteredByEventTypeAreRequested_ThenEventsUpToASpecificDateFilteredByEventTypeAreReturned()
    {
        var streamId = new TestStreamId(Guid.NewGuid().ToString());

        TimeProvider.SetUtcNow(new DateTime(2024, 6, 10, 12, 10, 25));
        await DomainService.SaveEvents(streamId, [
            new SomethingHappenedEvent("Something1"),
            new TestAggregateCreatedEvent(Guid.NewGuid().ToString(), "Test Name", "Test Description"),
        ], expectedEventSequence: 0);

        TimeProvider.SetUtcNow(new DateTime(2024, 6, 15, 17, 45, 48));
        await DomainService.SaveEvents(streamId, [
            new SomethingHappenedEvent("Something2"),
            new TestAggregateUpdatedEvent(Guid.NewGuid().ToString(), "Updated Name", "Updated Description")
        ], expectedEventSequence: 2);

        TimeProvider.SetUtcNow(new DateTime(2024, 6, 15, 17, 45, 49));
        await DomainService.SaveEvents(streamId, [
            new SomethingHappenedEvent("Something3"),
            new SomethingHappenedEvent("Something4")
        ], expectedEventSequence: 4);

        var result = await DomainService.GetEventsUpToDate(streamId, upToDate: new DateTimeOffset(new DateTime(2024, 6, 15, 17, 45, 48)), eventTypeFilter: [typeof(SomethingHappenedEvent)]);
        using (new AssertionScope())
        {
            result.Value!.Count.Should().Be(2);
            result.Value[0].Should().BeOfType<SomethingHappenedEvent>().Which.Something.Should().Be("Something1");
            result.Value[1].Should().BeOfType<SomethingHappenedEvent>().Which.Something.Should().Be("Something2");
        }
    }

    [Fact]
    public async Task GiveMultipleEventsStored_WhenOnlyEventsFromASpecificDateAreRequested_ThenEventsFromASpecificDateAreReturned()
    {
        var streamId = new TestStreamId(Guid.NewGuid().ToString());

        TimeProvider.SetUtcNow(new DateTime(2024, 6, 10, 12, 10, 25));
        await DomainService.SaveEvents(streamId, [
            new SomethingHappenedEvent("Something1"),
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

        var result = await DomainService.GetEventsFromDate(streamId, fromDate: new DateTimeOffset(new DateTime(2024, 6, 15, 17, 45, 48)));
        using (new AssertionScope())
        {
            result.Value!.Count.Should().Be(4);
            result.Value[0].Should().BeOfType<SomethingHappenedEvent>().Which.Something.Should().Be("Something3");
            result.Value[1].Should().BeOfType<SomethingHappenedEvent>().Which.Something.Should().Be("Something4");
            result.Value[2].Should().BeOfType<SomethingHappenedEvent>().Which.Something.Should().Be("Something5");
            result.Value[3].Should().BeOfType<SomethingHappenedEvent>().Which.Something.Should().Be("Something6");
        }
    }

    [Fact]
    public async Task GiveMultipleEventsStored_WhenOnlyEventsFromASpecificDateFilteredByEventTypeAreRequested_ThenEventsFromASpecificDateFilteredByEventTypeAreReturned()
    {
        var streamId = new TestStreamId(Guid.NewGuid().ToString());

        TimeProvider.SetUtcNow(new DateTime(2024, 6, 10, 12, 10, 25));
        await DomainService.SaveEvents(streamId, [
            new SomethingHappenedEvent("Something1"),
            new TestAggregateCreatedEvent(Guid.NewGuid().ToString(), "Test Name", "Test Description"),
        ], expectedEventSequence: 0);

        TimeProvider.SetUtcNow(new DateTime(2024, 6, 15, 17, 45, 48));
        await DomainService.SaveEvents(streamId, [
            new SomethingHappenedEvent("Something2"),
            new TestAggregateUpdatedEvent(Guid.NewGuid().ToString(), "Updated Name", "Updated Description")
        ], expectedEventSequence: 2);

        TimeProvider.SetUtcNow(new DateTime(2024, 6, 15, 17, 45, 49));
        await DomainService.SaveEvents(streamId, [
            new SomethingHappenedEvent("Something3"),
            new SomethingHappenedEvent("Something4")
        ], expectedEventSequence: 4);

        var result = await DomainService.GetEventsFromDate(streamId, fromDate: new DateTimeOffset(new DateTime(2024, 6, 15, 17, 45, 48)), eventTypeFilter: [typeof(SomethingHappenedEvent)]);
        using (new AssertionScope())
        {
            result.Value!.Count.Should().Be(3);
            result.Value[0].Should().BeOfType<SomethingHappenedEvent>().Which.Something.Should().Be("Something2");
            result.Value[1].Should().BeOfType<SomethingHappenedEvent>().Which.Something.Should().Be("Something3");
            result.Value[2].Should().BeOfType<SomethingHappenedEvent>().Which.Something.Should().Be("Something4");
        }
    }

    [Fact]
    public async Task GiveMultipleEventsStored_WhenOnlyEventsBetweenSpecificDatesAreRequested_ThenEventsBetweenSpecificDatesAreReturned()
    {
        var streamId = new TestStreamId(Guid.NewGuid().ToString());

        TimeProvider.SetUtcNow(new DateTime(2024, 6, 10, 12, 10, 25));
        await DomainService.SaveEvents(streamId, [
            new SomethingHappenedEvent("Something1"),
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

        var result = await DomainService.GetEventsBetweenDates(streamId,
            fromDate: new DateTimeOffset(new DateTime(2024, 6, 10, 12, 10, 25)),
            toDate: new DateTimeOffset(new DateTime(2024, 6, 15, 17, 45, 48)));
        using (new AssertionScope())
        {
            result.Value!.Count.Should().Be(4);
            result.Value[0].Should().BeOfType<SomethingHappenedEvent>().Which.Something.Should().Be("Something1");
            result.Value[1].Should().BeOfType<SomethingHappenedEvent>().Which.Something.Should().Be("Something2");
            result.Value[2].Should().BeOfType<SomethingHappenedEvent>().Which.Something.Should().Be("Something3");
            result.Value[3].Should().BeOfType<SomethingHappenedEvent>().Which.Something.Should().Be("Something4");
        }
    }

    [Fact]
    public async Task GiveMultipleEventsStored_WhenOnlyEventsBetweenSpecificDatesFilteredByEventTypeAreRequested_ThenEventsBetweenSpecificDatesFilteredByEventTypeAreReturned()
    {
        var streamId = new TestStreamId(Guid.NewGuid().ToString());

        TimeProvider.SetUtcNow(new DateTime(2024, 6, 10, 12, 10, 25));
        await DomainService.SaveEvents(streamId, [
            new SomethingHappenedEvent("Something1"),
            new TestAggregateCreatedEvent(Guid.NewGuid().ToString(), "Test Name", "Test Description"),
        ], expectedEventSequence: 0);

        TimeProvider.SetUtcNow(new DateTime(2024, 6, 15, 17, 45, 48));
        await DomainService.SaveEvents(streamId, [
            new SomethingHappenedEvent("Something2"),
            new TestAggregateUpdatedEvent(Guid.NewGuid().ToString(), "Updated Name", "Updated Description")
        ], expectedEventSequence: 2);

        TimeProvider.SetUtcNow(new DateTime(2024, 6, 15, 17, 45, 49));
        await DomainService.SaveEvents(streamId, [
            new SomethingHappenedEvent("Something3"),
            new SomethingHappenedEvent("Something4")
        ], expectedEventSequence: 4);

        var result = await DomainService.GetEventsBetweenDates(streamId,
            fromDate: new DateTimeOffset(new DateTime(2024, 6, 10, 12, 10, 25)),
            toDate: new DateTimeOffset(new DateTime(2024, 6, 15, 17, 45, 48)),
            eventTypeFilter: [typeof(SomethingHappenedEvent)]);
        using (new AssertionScope())
        {
            result.Value!.Count.Should().Be(2);
            result.Value[0].Should().BeOfType<SomethingHappenedEvent>().Which.Something.Should().Be("Something1");
            result.Value[1].Should().BeOfType<SomethingHappenedEvent>().Which.Something.Should().Be("Something2");
        }
    }
}
