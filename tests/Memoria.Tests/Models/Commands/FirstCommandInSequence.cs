using Memoria.Commands;

namespace Memoria.Tests.Models.Commands;

public record FirstCommandInSequence(string Name) : ICommand<string>;
