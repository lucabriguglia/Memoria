using Memoria.Commands;

namespace OpenCqrs.Tests.Models.Commands;

public record CommandWithResult(string Name) : ICommand<string>;
