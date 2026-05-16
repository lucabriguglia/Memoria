# Run a sequence of commands

A `CommandSequence<TResponse>` runs several commands in order through the dispatcher, threading the previous results into each handler. Use it when steps belong together logically and each step can react to what came before.

All commands in the sequence must share the same response type.

## Define the sequence

```C#
public class FirstCommand : ICommand<string> { }
public class SecondCommand : ICommand<string> { }
public class ThirdCommand : ICommand<string> { }

public class MyCommandSequence : CommandSequence<string>
{
    public MyCommandSequence()
    {
        AddCommand(new FirstCommand());
        AddCommand(new SecondCommand());
        AddCommand(new ThirdCommand());
    }
}
```

## Implement the handlers

Each handler implements `ISequenceCommandHandlerAsync<TCommand, TResponse>` and receives the results of every prior step:

```C#
public class FirstCommandHandler : ISequenceCommandHandlerAsync<FirstCommand, string>
{
    public Task<Result<string>> HandleAsync(
        FirstCommand command,
        IEnumerable<Result<string>> previousResults)
    {
        return Task.FromResult(Result.Ok("First result"));
    }
}

public class SecondCommandHandler : ISequenceCommandHandlerAsync<SecondCommand, string>
{
    public Task<Result<string>> HandleAsync(
        SecondCommand command,
        IEnumerable<Result<string>> previousResults)
    {
        return Task.FromResult(Result.Ok("Second result"));
    }
}

public class ThirdCommandHandler : ISequenceCommandHandlerAsync<ThirdCommand, string>
{
    public Task<Result<string>> HandleAsync(
        ThirdCommand command,
        IEnumerable<Result<string>> previousResults)
    {
        return Task.FromResult(Result.Ok("Third result"));
    }
}
```

## Dispatch

```C#
var sequenceResult = await dispatcher.SendSequence(new MyCommandSequence());
```

The result holds every step's `Result<string>` in order. Two optional flags shape execution:

- **`stopOnFirstFailure: true`** — abort the sequence on the first failure instead of running every step.
- **`validateCommands: true`** — run [command validation](validate-commands.md) on every step.

```C#
var sequenceResult = await dispatcher.SendSequence(
    new MyCommandSequence(),
    stopOnFirstFailure: true,
    validateCommands: true);
```

## Related

- [Quickstart: Mediator](../getting-started/quickstart-mediator.md)
- [Validate commands](validate-commands.md)
