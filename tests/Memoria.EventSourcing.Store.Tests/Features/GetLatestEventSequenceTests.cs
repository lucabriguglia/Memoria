using FluentAssertions;
using Memoria.EventSourcing.Store.Tests.Models.Aggregates;
using Memoria.EventSourcing.Store.Tests.Models.Streams;
using Xunit;

namespace Memoria.EventSourcing.Store.Tests.Features;

public abstract class GetLatestEventSequenceTests(IDomainServiceFactory domainServiceFactory) : TestBase(domainServiceFactory)
{
    [Fact]
    public async Task GivenNoEventsSaved_TheLatestEventSequenceReturnedIsZero()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);

        var latestEventSequence = await DomainService.GetLatestEventSequence(streamId);

        latestEventSequence.Value.Should().Be(0);
    }

    [Fact]
    public async Task GivenMultipleEventsSaved_TheLatestEventSequenceIsReturned()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);
        var aggregate = new TestAggregate1(id, "Test Name", "Test Description");
        aggregate.Update("Updated Name", "Updated Description");
        aggregate.Update("Updated Name 2", "Updated Description 2");
        aggregate.Update("Updated Name 3", "Updated Description 3");

        await DomainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);
        var latestEventSequence = await DomainService.GetLatestEventSequence(streamId);

        latestEventSequence.Value.Should().Be(4);
    }

    [Fact]
    public async Task GivenMultipleEventsSaved_WhenFilteredByEventProperty_TheLatestEventSequenceMatchingTheFilterIsReturned()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregateIdWithCustomPropertyFilter(id, propertyName: "Description",
            propertyValue: "Updated Description 2");
        var aggregate = new TestAggregate1(id, "Test Name", "Test Description");
        aggregate.Update("Updated Name", "Updated Description");
        aggregate.Update("Updated Name 2", "Updated Description 2");
        aggregate.Update("Updated Name 3", "Updated Description 3");

        await DomainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);
        var latestEventSequence = await DomainService.GetLatestEventSequence(streamId,
            eventPropertyFilter: aggregateId.EventPropertyFilter);

        latestEventSequence.Value.Should().Be(3);
    }

    [Fact]
    public async Task GivenMultipleEventsSaved_WhenFilteredByMultipleEventProperties_TheLatestEventSequenceMatchingAllPropertiesIsReturned()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);
        var aggregate = new TestAggregate1(id, "Match", "Match");
        aggregate.Update("Match", "NoMatch");
        aggregate.Update("NoMatch", "Match");
        aggregate.Update("Match", "Match");
        aggregate.Update("NoMatch", "NoMatch");

        await DomainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);
        var latestEventSequence = await DomainService.GetLatestEventSequence(streamId,
            eventPropertyFilter: new Dictionary<string, string>
            {
                { "Name", "Match" },
                { "Description", "Match" }
            });

        latestEventSequence.Value.Should().Be(4);
    }

    [Fact]
    public async Task GivenMultipleEventsSaved_WhenFilteredByEventPropertyThatMatchesNoEvents_ZeroIsReturned()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregateId = new TestAggregate1Id(id);
        var aggregate = new TestAggregate1(id, "Test Name", "Test Description");
        aggregate.Update("Updated Name", "Updated Description");
        aggregate.Update("Updated Name 2", "Updated Description 2");

        await DomainService.SaveAggregate(streamId, aggregateId, aggregate, expectedEventSequence: 0);
        var latestEventSequence = await DomainService.GetLatestEventSequence(streamId,
            eventPropertyFilter: new Dictionary<string, string> { { "Description", "Does Not Exist" } });

        latestEventSequence.Value.Should().Be(0);
    }
}
