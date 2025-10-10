using Memoria.Examples.EventSourcing.Cosmos.Aggregates;
using Memoria.Queries;

namespace Memoria.Examples.EventSourcing.Cosmos.Queries;

public record GetOrderQuery(Guid CustomerId, Guid OrderId) : IQuery<Order?>;
