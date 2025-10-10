using Memoria.Commands;

namespace Memoria.EventSourcing.Store.Tests.Models.Commands;

public record CreateTestAggregateCommand(string Id, string Name, string Description) : ICommand;
