using Memoria.EventSourcing.Domain;

namespace Memoria.EventSourcing.Store.Tests.Models.Aggregates;

public class TestAggregate1Id(string testAggregateId) : IAggregateId<TestAggregate1>
{
    public string Id => $"test-aggregate-1:{testAggregateId}";
}
