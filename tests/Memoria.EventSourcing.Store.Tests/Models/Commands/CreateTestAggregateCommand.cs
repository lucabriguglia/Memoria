using Memoria.Commands;

namespace OpenCqrs.EventSourcing.Store.Tests.Models.Commands;

public record CreateTestAggregateCommand(string Id, string Name, string Description) : ICommand;
