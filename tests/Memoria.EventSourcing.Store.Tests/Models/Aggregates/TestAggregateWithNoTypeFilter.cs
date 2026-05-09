using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.Tests.Models.Events;

namespace Memoria.EventSourcing.Store.Tests.Models.Aggregates;

[AggregateType("TestAggregateWithNoTypeFilter")]
public class TestAggregateWithNoTypeFilter : AggregateRoot
{
    public override Type[] EventTypeFilter { get; } = [];

    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;

    public TestAggregateWithNoTypeFilter()
    {
    }

    public TestAggregateWithNoTypeFilter(string id, string name, string description)
    {
        Add(new TestAggregateCreatedEvent(id, name, description));
    }

    public void Update(string name, string description)
    {
        Add(new TestAggregateUpdatedEvent(Id, name, description));
    }

    protected override bool Apply<T>(T @event)
    {
        return @event switch
        {
            TestAggregateCreatedEvent testAggregateCreated => Apply(testAggregateCreated),
            TestAggregateUpdatedEvent testAggregateUpdated => Apply(testAggregateUpdated),
            SomethingHappenedEvent2 somethingHappened2 => Apply(somethingHappened2),
            _ => false
        };
    }

    private bool Apply(TestAggregateCreatedEvent @event)
    {
        Id = @event.Id;
        Name = @event.Name;
        Description = @event.Description;

        return true;
    }

    private bool Apply(TestAggregateUpdatedEvent @event)
    {
        Name = @event.Name;
        Description = @event.Description;

        return true;
    }

    private bool Apply(SomethingHappenedEvent2 @event)
    {
        Name = @event.Something2;

        return true;
    }
}