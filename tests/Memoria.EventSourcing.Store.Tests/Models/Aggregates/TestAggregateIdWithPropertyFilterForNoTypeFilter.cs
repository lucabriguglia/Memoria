using Memoria.EventSourcing.Domain;

namespace Memoria.EventSourcing.Store.Tests.Models.Aggregates;

public class TestAggregateIdWithPropertyFilterForNoTypeFilter(string testAggregateId, string something)
    : IAggregateId<TestAggregateWithNoTypeFilter>
{
    public string Id => $"test-aggregate-with-filter-for-no-type-filter:{testAggregateId}:{something}";
    public IDictionary<string, string>? EventPropertyFilter { get; } = new Dictionary<string, string>{{"Something2", something}};
}