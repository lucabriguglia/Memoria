using Memoria.EventSourcing.Domain;

namespace Memoria.EventSourcing.Store.Tests.Models.Aggregates;

public class TestAggregateIdWithFilter(string testAggregateId, string something) : IAggregateId<TestAggregate1>
{
    public string Id => $"test-aggregate-with-filter:{testAggregateId}:{something}";
    public string[]? EventPropertyFilter { get; } = [$"Something2={something}]"];
}