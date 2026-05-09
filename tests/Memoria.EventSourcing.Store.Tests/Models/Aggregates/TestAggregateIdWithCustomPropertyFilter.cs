using Memoria.EventSourcing.Domain;

namespace Memoria.EventSourcing.Store.Tests.Models.Aggregates;

public class TestAggregateIdWithCustomPropertyFilter(string testAggregateId, string propertyName, string propertyValue)
    : IAggregateId<TestAggregate1>
{
    public string Id => $"test-aggregate-with-filter:{testAggregateId}:{propertyValue}";
    public IDictionary<string, string>? EventPropertyFilter { get; } = new Dictionary<string, string>{{propertyName, propertyValue}};
}