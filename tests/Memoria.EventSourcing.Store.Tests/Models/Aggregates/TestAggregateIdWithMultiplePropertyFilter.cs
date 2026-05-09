using Memoria.EventSourcing.Domain;

namespace Memoria.EventSourcing.Store.Tests.Models.Aggregates;

public class TestAggregateIdWithMultiplePropertyFilter(string testAggregateId, string name, string description)
    : IAggregateId<TestAggregateWithMultiplePropertyFilter>
{
    public string Id => $"test-aggregate-with-multiple-property-filter:{testAggregateId}:{name}:{description}";
    public string[]? EventPropertyFilter { get; } = [$"Name={name}", $"Description={description}"];
}