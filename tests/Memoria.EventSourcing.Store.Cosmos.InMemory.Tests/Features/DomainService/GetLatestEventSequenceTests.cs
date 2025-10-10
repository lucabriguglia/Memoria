namespace Memoria.EventSourcing.Store.Cosmos.InMemory.Tests.Features.DomainService;

public class GetLatestEventSequenceTests() : Store.Tests.Features.GetLatestEventSequenceTests(new DomainServiceFactory());
