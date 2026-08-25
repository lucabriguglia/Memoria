namespace Memoria.EventSourcing.Store.Cosmos.Tests.Features.DomainService;

public class GetInMemoryProjectionTests()
    : Store.Tests.Features.GetInMemoryProjectionTests(new CosmosDomainServiceFactory());
