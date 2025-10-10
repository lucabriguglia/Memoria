using Memoria.Commands;

namespace Memoria.EventSourcing.Store.Tests.Models.Commands;

public record UpdateTestAggregateCommand(string Id, string Name, string Description) : ICommand;
