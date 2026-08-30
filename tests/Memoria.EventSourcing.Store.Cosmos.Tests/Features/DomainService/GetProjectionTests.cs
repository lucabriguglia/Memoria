using Xunit;

namespace Memoria.EventSourcing.Store.Cosmos.Tests.Features.DomainService;

[Trait("Category", "Emulator")]
public class GetProjectionTests()
    : Store.Tests.Features.GetProjectionTests(new CosmosDomainServiceFactory());
