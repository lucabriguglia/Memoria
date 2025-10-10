using Memoria.EventSourcing.Domain;
using OpenCqrs.Examples.EventSourcing.Cosmos.DomainEvents;

namespace OpenCqrs.Examples.EventSourcing.Cosmos.Aggregates;

[AggregateType("Order")]
public class Order : AggregateRoot
{
    public override Type[] EventTypeFilter { get; } =
    [
        typeof(OrderPlacedEvent)
    ];

    public Guid OrderId { get; private set; }
    public decimal Amount { get; private set; }

    public Order() { }

    public Order(Guid orderId, decimal amount)
    {
        Add(new OrderPlacedEvent(OrderId = orderId, Amount = amount));
    }

    protected override bool Apply<T>(T @event)
    {
        return @event switch
        {
            OrderPlacedEvent orderPlaced => Apply(orderPlaced),
            _ => false
        };
    }

    private bool Apply(OrderPlacedEvent @event)
    {
        OrderId = @event.OrderId;
        Amount = @event.Amount;

        return true;
    }
}
