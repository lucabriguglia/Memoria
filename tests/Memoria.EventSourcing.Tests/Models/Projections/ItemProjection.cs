using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Tests.Models.Events;

namespace Memoria.EventSourcing.Tests.Models.Projections;

/// <summary>
/// A read model that rebuilds item state from events. It has no way to add or stage events.
/// </summary>
public class ItemProjection : Projection
{
    public override Type[] EventTypeFilter { get; } =
    [
        typeof(ItemCreatedEvent),
        typeof(ItemRenamedEvent)
    ];

    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;

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
