namespace Memoria.Messaging.RabbitMq.Tests;

public class RabbitMqMessagingProviderTests()
    : Memoria.Messaging.Tests.Features.MessagingProviderTests(new RabbitMqMessagingProviderFactory());