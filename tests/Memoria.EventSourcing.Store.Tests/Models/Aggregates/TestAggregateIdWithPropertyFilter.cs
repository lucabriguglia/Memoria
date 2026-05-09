using Memoria.EventSourcing.Domain;

namespace Memoria.EventSourcing.Store.Tests.Models.Aggregates;

public class TestAggregateIdWithPropertyFilter(string testAggregateId, string something) : IAggregateId<TestAggregate1>
{
    public string Id => $"test-aggregate-with-filter:{testAggregateId}:{something}";
    public IDictionary<string, string>? EventPropertyFilter { get; } = new Dictionary<string, string>{{"Something2", something}};
}