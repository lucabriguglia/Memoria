using Memoria.Messaging.Tests;

namespace Memoria.Messaging.RabbitMq.Tests;

public class RabbitMqMessagingProviderFactory : IMessagingProviderFactory
{
    public IMessagingProvider CreateMessagingProvider()
    {
        var mockRabbitMqTestHelper = new MockRabbitMqTestHelper();
        return new RabbitMqMessagingProvider(mockRabbitMqTestHelper.MockOptions, mockRabbitMqTestHelper.MockConnection);
    }
}