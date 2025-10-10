using Memoria.Commands;
using Memoria.Messaging;
using Memoria.Results;
using OpenCqrs.Examples.Messaging.RabbitMq.Messages;
using OpenCqrs.Messaging;

namespace OpenCqrs.Examples.Messaging.RabbitMq.Commands.Handlers;

public class PlaceOrderCommandHandler : ICommandHandler<PlaceOrderCommand, CommandResponse>
{
    public async Task<Result<CommandResponse>> Handle(PlaceOrderCommand command, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        // Business logic to place the order would go here.

        var commandResponse = new CommandResponse
        {
            Messages = new List<Message>
            {
                new TestQueueMessage
                {
                    TestData = "Test Queue Message 1 from PlaceOrderCommandHandler",
                    QueueName = "test-queue"
                },
                new TestQueueMessage
                {
                    TestData = "Test Queue Message 2 from PlaceOrderCommandHandler",
                    QueueName = "test-queue"
                }
            }
        };

        return commandResponse;
    }
}
