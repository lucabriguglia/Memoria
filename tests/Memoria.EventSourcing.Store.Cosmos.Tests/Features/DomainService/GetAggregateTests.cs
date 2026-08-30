using Xunit;

namespace Memoria.EventSourcing.Store.Cosmos.Tests.Features.DomainService;

[Trait("Category", "Emulator")]
public class GetAggregateTests() : Store.Tests.Features.GetAggregateTests(new CosmosDomainServiceFactory());