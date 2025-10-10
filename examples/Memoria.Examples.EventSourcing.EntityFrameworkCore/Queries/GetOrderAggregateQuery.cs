using Memoria.Examples.EventSourcing.EntityFrameworkCore.Aggregates;
using Memoria.Queries;

namespace Memoria.Examples.EventSourcing.EntityFrameworkCore.Queries;

public record GetOrderAggregateQuery(Guid CustomerId, Guid OrderId) : IQuery<Order?>;
