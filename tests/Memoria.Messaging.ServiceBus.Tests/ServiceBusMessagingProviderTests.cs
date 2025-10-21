namespace Memoria.Messaging.ServiceBus.Tests;

public class MessagingProviderTests()
    : Memoria.Messaging.Tests.Features.MessagingProviderTests(new ServiceBusMessagingProviderFactory());