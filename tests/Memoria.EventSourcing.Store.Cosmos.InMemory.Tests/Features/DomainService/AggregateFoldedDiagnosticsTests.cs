namespace Memoria.EventSourcing.Store.Cosmos.InMemory.Tests.Features.DomainService;

public class AggregateFoldedDiagnosticsTests()
    : Store.Tests.Features.AggregateFoldedDiagnosticsTests(new InMemoryCosmosDomainServiceFactory());
