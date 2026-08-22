namespace Memoria.EventSourcing.Store.Cosmos.Tests.Features.DomainService;

public class GetProjectionTests()
    : Store.Tests.Features.GetProjectionTests(new CosmosDomainServiceFactory());
