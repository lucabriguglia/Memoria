using Memoria.Queries;
using OpenCqrs.Examples.EventSourcing.Cosmos.Aggregates;

namespace OpenCqrs.Examples.EventSourcing.Cosmos.Queries;

public record GetOrderQuery(Guid CustomerId, Guid OrderId) : IQuery<Order?>;
