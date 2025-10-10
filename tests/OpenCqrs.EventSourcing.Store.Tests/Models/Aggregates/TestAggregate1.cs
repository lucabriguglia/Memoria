using Memoria.EventSourcing.Domain;
using OpenCqrs.EventSourcing.Store.Tests.Models.Events;

namespace OpenCqrs.EventSourcing.Store.Tests.Models.Aggregates;

[AggregateType("TestAggregate1")]
public class TestAggregate1 : AggregateRoot
{
    public override Type[] EventTypeFilter { get; } =
    [
        typeof(TestAggregateCreatedEvent),
        typeof(TestAggregateUpdatedEvent),
        typeof(SomethingHappenedEvent)
    ];

    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;

    public TestAggregate1()
    {
    }

    public TestAggregate1(string id, string name, string description)
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
}
