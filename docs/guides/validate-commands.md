# Validate commands

Memoria can run a validator over a command before it reaches its handler. If validation fails, the handler is not called and the dispatcher returns a `Result` whose `Error.Details` lists every failure.

This guide assumes you have FluentValidation registered — see [Configuration: Validation](../reference/configuration/validation.md).

## Opt in per call

Validation is opt-in. Set `validateCommand: true` on any dispatcher method:

```C#
var result = await dispatcher.Send(command, validateCommand: true);
var result = await dispatcher.SendAndPublish(command, validateCommand: true);
```

For [command sequences](command-sequences.md), use `validateCommands: true` on `SendSequence`.

## What a validation failure looks like

```C#
{
    "IsSuccess": false,
    "Value": null,
    "Error": {
        "Message": "Validation failed",
        "Details": [
            "Name must not be empty",
            "Age must be greater than 0"
        ]
    }
}
```

## Writing the validator

Use FluentValidation as you would in any other project:

```C#
public class CreateProductValidator : AbstractValidator<CreateProduct>
{
    public CreateProductValidator()
    {
        RuleFor(c => c.Name).NotEmpty();
    }
}
```

`AddMemoriaFluentValidation(typeof(...))` scans the supplied assembly for `AbstractValidator<>` subclasses and registers them automatically.

## Related

- [Quickstart: Mediator](../getting-started/quickstart-mediator.md)
- [Configuration: Validation](../reference/configuration/validation.md)
