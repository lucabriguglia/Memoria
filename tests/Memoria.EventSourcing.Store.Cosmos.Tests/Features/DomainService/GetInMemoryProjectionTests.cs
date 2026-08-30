using Xunit;

namespace Memoria.EventSourcing.Store.Cosmos.Tests.Features.DomainService;

[Trait("Category", "Emulator")]
public class GetInMemoryProjectionTests()
    : Store.Tests.Features.GetInMemoryProjectionTests(new CosmosDomainServiceFactory());
