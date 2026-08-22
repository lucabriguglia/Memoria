namespace Memoria.EventSourcing.Store.Cosmos.InMemory.Tests.Features.DomainService;

public class GetProjectionTests()
    : Store.Tests.Features.GetProjectionTests(new InMemoryCosmosDomainServiceFactory());
