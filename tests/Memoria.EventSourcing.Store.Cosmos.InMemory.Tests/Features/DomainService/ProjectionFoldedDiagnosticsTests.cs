namespace Memoria.EventSourcing.Store.Cosmos.InMemory.Tests.Features.DomainService;

public class ProjectionFoldedDiagnosticsTests()
    : Store.Tests.Features.ProjectionFoldedDiagnosticsTests(new InMemoryCosmosDomainServiceFactory());
