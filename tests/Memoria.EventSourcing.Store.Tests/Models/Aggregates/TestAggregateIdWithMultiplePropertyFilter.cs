using Memoria.EventSourcing.Domain;

namespace Memoria.EventSourcing.Store.Tests.Models.Aggregates;

public class TestAggregateIdWithMultiplePropertyFilter(string testAggregateId, string name, string description)
    : IAggregateId<TestAggregateWithMultiplePropertyFilter>
{
    public string Id => $"test-aggregate-with-multiple-property-filter:{testAggregateId}:{name}:{description}";
    public IDictionary<string, string>? EventPropertyFilter { get; } = new  Dictionary<string, string>
    {
        { "Name", name },
        { "Description", description }
    };
}
