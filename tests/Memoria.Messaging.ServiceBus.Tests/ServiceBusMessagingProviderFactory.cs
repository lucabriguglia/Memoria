using Memoria.Messaging.Tests;

namespace Memoria.Messaging.ServiceBus.Tests;

public class ServiceBusMessagingProviderFactory : IMessagingProviderFactory
{
    public IMessagingProvider CreateMessagingProvider()
    {
        var mockServiceBusTestHelper = new MockServiceBusTestHelper();
        return new MessagingProvider(mockServiceBusTestHelper.MockServiceBusClient);
    }
}