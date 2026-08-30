using Xunit;

namespace Memoria.EventSourcing.Store.Cosmos.Tests.Features.DomainService;

[Trait("Category", "Emulator")]
public class UpdateAggregateTests() : Store.Tests.Features.UpdateAggregateTests(new CosmosDomainServiceFactory());