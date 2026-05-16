# Configuration: RabbitMQ

To use RabbitMQ messaging, install and register the **Memoria.Messaging.RabbitMq** package:

```C#
services.AddMemoriaRabbitMq(options =>
{
    options.ConnectionString = connectionString;
});
```

For local development and tests without a RabbitMQ broker, use **Memoria.Messaging.RabbitMq.InMemory**.

## Related

- [Memoria Core](memoria.md)
- [Service Bus](messaging-servicebus.md)
