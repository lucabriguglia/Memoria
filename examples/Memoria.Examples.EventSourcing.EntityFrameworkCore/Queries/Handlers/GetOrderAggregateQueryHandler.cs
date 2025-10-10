using Memoria.EventSourcing;
using Memoria.Examples.EventSourcing.EntityFrameworkCore.Aggregates;
using Memoria.Examples.EventSourcing.EntityFrameworkCore.Streams;
using Memoria.Queries;
using Memoria.Results;

namespace Memoria.Examples.EventSourcing.EntityFrameworkCore.Queries.Handlers;

public class GetOrderAggregateQueryHandler(IDomainService domainService) : IQueryHandler<GetOrderAggregateQuery, Order?>
{
    public async Task<Result<Order?>> Handle(GetOrderAggregateQuery query, CancellationToken cancellationToken = default)
    {
        var customerStreamId = new CustomerStreamId(query.CustomerId);
        var orderAggregateId = new OrderId(query.OrderId);

        return await domainService.GetAggregate(customerStreamId, orderAggregateId, ReadMode.SnapshotOnly, cancellationToken);
    }
}
