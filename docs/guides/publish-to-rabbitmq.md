# Publish notifications and messages to RabbitMQ

The mechanism for publishing in-process notifications and bus messages is identical across messaging providers — only the registration changes. If you've read [Publish to Service Bus](publish-to-service-bus.md), the only difference is the package and the `Add…` call.

Register `Memoria.Messaging.RabbitMq` instead of Service Bus — see [Configuration: RabbitMQ](../reference/configuration/messaging-rabbitmq.md). Everything else (the `CommandResponse` shape, `SendAndPublish`, the success short-circuit) is unchanged.

For a worked example of the command/handler/dispatch pattern, see [Publish to Service Bus](publish-to-service-bus.md#dispatch).

## Related

- [Configuration: RabbitMQ](../reference/configuration/messaging-rabbitmq.md)
- [Publish to Service Bus](publish-to-service-bus.md)
