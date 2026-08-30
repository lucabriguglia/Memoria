using Xunit;

namespace Memoria.EventSourcing.Store.Cosmos.Tests.Features.DomainService;

[Trait("Category", "Emulator")]
public class GetInMemoryAggregateTests()
    : Store.Tests.Features.GetInMemoryAggregateTests(new CosmosDomainServiceFactory());