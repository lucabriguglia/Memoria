using Memoria.Commands;
using Memoria.EventSourcing;
using Memoria.Results;
using OpenCqrs.EventSourcing;
using OpenCqrs.Examples.EventSourcing.Cosmos.Aggregates;
using OpenCqrs.Examples.EventSourcing.Cosmos.Streams;

namespace OpenCqrs.Examples.EventSourcing.Cosmos.Commands.Handlers;

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
