namespace Memoria.EventSourcing.Store.Cosmos.InMemory.Tests.Features.DomainService;

public class GetInMemoryProjectionTests()
    : Store.Tests.Features.GetInMemoryProjectionTests(new InMemoryCosmosDomainServiceFactory());
