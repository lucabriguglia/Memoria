using FluentAssertions;
using OpenCqrs.EventSourcing.Store.Tests.Models.Aggregates;
using OpenCqrs.EventSourcing.Store.Tests.Models.Streams;
using Xunit;

namespace OpenCqrs.EventSourcing.Store.Tests.Features;

public class GetLatestEventSequenceTests(IDomainServiceFactory domainServiceFactory) : TestBase(domainServiceFactory)
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
}
