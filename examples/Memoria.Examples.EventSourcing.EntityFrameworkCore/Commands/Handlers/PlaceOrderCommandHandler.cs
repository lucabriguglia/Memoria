using Memoria.Commands;
using Memoria.EventSourcing;
using Memoria.Examples.EventSourcing.EntityFrameworkCore.Aggregates;
using Memoria.Examples.EventSourcing.EntityFrameworkCore.Streams;
using Memoria.Results;

namespace Memoria.Examples.EventSourcing.EntityFrameworkCore.Commands.Handlers;

public class PlaceOrderCommandHandler(IDomainService domainService) : ICommandHandler<PlaceOrderCommand>
{
    public async Task<Result> Handle(PlaceOrderCommand command, CancellationToken cancellationToken = default)
    {
        var customerStreamId = new CustomerStreamId(command.CustomerId);
        var orderAggregateId = new OrderId(command.OrderId);

        var orderAggregate = new Order(command.OrderId, command.Amount);

        return await domainService.SaveAggregate(customerStreamId, orderAggregateId, orderAggregate, expectedEventSequence: 0, cancellationToken);
    }
}
