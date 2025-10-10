using Memoria.EventSourcing.Domain;

namespace Memoria.EventSourcing.Store.Tests.Models.Aggregates;

public class TestAggregate2Id(string testAggregateId) : IAggregateId<TestAggregate2>
{
    public string Id => $"test-aggregate-2:{testAggregateId}";
}
