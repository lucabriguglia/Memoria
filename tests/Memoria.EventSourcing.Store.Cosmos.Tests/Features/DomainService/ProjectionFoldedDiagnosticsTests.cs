using Xunit;

namespace Memoria.EventSourcing.Store.Cosmos.Tests.Features.DomainService;

[Trait("Category", "Emulator")]
public class ProjectionFoldedDiagnosticsTests()
    : Store.Tests.Features.ProjectionFoldedDiagnosticsTests(new CosmosDomainServiceFactory());
