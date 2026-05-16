# Use a custom command handler

By default the dispatcher resolves the registered `ICommandHandler<>` for the command type. You can bypass that and supply a delegate yourself when you need to — for example, to call a domain service directly, to compose existing handlers, or in test code.

## Opt in per call

Pass a delegate as the second argument to `Send` or `SendAndPublish`:

```C#
var result = await dispatcher.Send(
    command,
    () => _somethingService.DoSomethingAsync(command));

var result = await dispatcher.SendAndPublish(
    command,
    () => _somethingService.DoSomethingAndPublishAsync(command));
```

The delegate's return type must match what the registered handler would return — `Task<Result>` for `ICommand`, `Task<Result<TResponse>>` for `ICommand<TResponse>`.

## Why you'd want to

- **Compose existing services** — when the work is already implemented somewhere else, you don't need a thin pass-through handler.
- **Inline test code** — set up exactly the behaviour the test needs without registering a one-off handler.
- **A/B experiments** — pick between two implementations at dispatch time.

For everything else, the standard `ICommandHandler<>` discovered by `AddMemoria` is the right choice.

## Related

- [Quickstart: Mediator](../getting-started/quickstart-mediator.md)
