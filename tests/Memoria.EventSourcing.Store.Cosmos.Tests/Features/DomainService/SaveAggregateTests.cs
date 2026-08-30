using Xunit;

namespace Memoria.EventSourcing.Store.Cosmos.Tests.Features.DomainService;

[Trait("Category", "Emulator")]
public class SaveAggregateTests() : Store.Tests.Features.SaveAggregateTests(new CosmosDomainServiceFactory());