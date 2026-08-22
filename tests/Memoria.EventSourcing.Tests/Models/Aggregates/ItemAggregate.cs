using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Tests.Models.Events;

namespace Memoria.EventSourcing.Tests.Models.Aggregates;

/// <summary>
/// A write model that stages events via <c>Add</c>, used to confirm aggregate behaviour is
/// preserved after moving shared concerns onto <see cref="EventSourcedModel"/>.
/// </summary>
[AggregateType("Item")]
public class ItemAggregate : AggregateRoot
{
    public override Type[] EventTypeFilter { get; } =
    [
        typeof(ItemCreatedEvent),
        typeof(ItemRenamedEvent)
    ];

    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;

    public ItemAggregate()
    {
    }

    public ItemAggregate(string id, string name)
    {
        Add(new ItemCreatedEvent(id, name));
    }

    public void Rename(string name)
    {
        Add(new ItemRenamedEvent(Id, name));
    }

    protected override bool Apply<T>(T @event)
    {
        return @event switch
        {
            ItemCreatedEvent created => Apply(created),
            ItemRenamedEvent renamed => Apply(renamed),
            _ => false
        };
    }

    private bool Apply(ItemCreatedEvent @event)
    {
        Id = @event.Id;
        Name = @event.Name;

        return true;
    }

    private bool Apply(ItemRenamedEvent @event)
    {
        Name = @event.Name;

        return true;
    }
}
