namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Tests.Features.DomainService;

public class ProjectionFoldedDiagnosticsTests()
    : Store.Tests.Features.ProjectionFoldedDiagnosticsTests(new DomainServiceFactory());
