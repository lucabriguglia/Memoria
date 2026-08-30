using Xunit;

namespace Memoria.EventSourcing.Store.Cosmos.Tests.Features.DomainService;

[Trait("Category", "Emulator")]
public class AggregateFoldedDiagnosticsTests()
    : Store.Tests.Features.AggregateFoldedDiagnosticsTests(new CosmosDomainServiceFactory());
