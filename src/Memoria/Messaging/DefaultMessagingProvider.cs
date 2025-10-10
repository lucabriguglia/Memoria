using OpenCqrs.Results;

namespace OpenCqrs.Messaging;

public class DefaultMessagingProvider : IMessagingProvider
{
    private static string NotImplementedMessage => "No messaging provider has been configured. Please configure a messaging provider such as Service Bus or RabbitMQ.";

    public Task<Result> SendQueueMessage<TMessage>(TMessage message, CancellationToken cancellationToken = default) where TMessage : IQueueMessage
    {
        throw new NotImplementedException(NotImplementedMessage);
    }

    public Task<Result> SendTopicMessage<TMessage>(TMessage message, CancellationToken cancellationToken = default) where TMessage : ITopicMessage
    {
        throw new NotImplementedException(NotImplementedMessage);
    }
}
