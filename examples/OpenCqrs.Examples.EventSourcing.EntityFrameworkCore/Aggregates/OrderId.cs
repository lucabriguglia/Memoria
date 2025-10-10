using Memoria.EventSourcing.Domain;

namespace OpenCqrs.Examples.EventSourcing.EntityFrameworkCore.Aggregates;

public class OrderId(Guid orderId) : IAggregateId<Order>
{
    public string Id => $"order:{orderId}";
}
