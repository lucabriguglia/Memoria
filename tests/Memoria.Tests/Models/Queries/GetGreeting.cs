using Memoria.Queries;

namespace Memoria.Tests.Models.Queries;

public record GetGreeting(string Name) : IQuery<string>;
