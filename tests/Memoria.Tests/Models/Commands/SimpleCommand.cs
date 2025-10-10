using Memoria.Commands;

namespace Memoria.Tests.Models.Commands;

public record SimpleCommand(string Name) : ICommand;
