using Memoria.Queries;
using OpenCqrs.Examples.EventSourcing.EntityFrameworkCore.Aggregates;

namespace OpenCqrs.Examples.EventSourcing.EntityFrameworkCore.Queries;

public record GetOrderAggregateQuery(Guid CustomerId, Guid OrderId) : IQuery<Order?>;
