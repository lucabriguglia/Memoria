# Configuration: Azure Service Bus

To use Service Bus messaging, install and register the **Memoria.Messaging.ServiceBus** package:

```C#
services.AddMemoriaServiceBus(new ServiceBusOptions
{
    ConnectionString = connectionString
});
```

For local development and tests without an Azure dependency, use **Memoria.Messaging.ServiceBus.InMemory**.

## Related

- [Memoria Core](memoria.md)
- [RabbitMQ](messaging-rabbitmq.md)
