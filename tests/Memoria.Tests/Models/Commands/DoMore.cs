using Memoria.Commands;

namespace Memoria.Tests.Models.Commands;

public record DoMore(string Name) : ICommand<CommandResponse>;
