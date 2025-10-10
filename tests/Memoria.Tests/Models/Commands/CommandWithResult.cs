using Memoria.Commands;

namespace Memoria.Tests.Models.Commands;

public record CommandWithResult(string Name) : ICommand<string>;
