using Xunit;

namespace Memoria.EventSourcing.Store.Cosmos.Tests.Features.DomainService;

[Trait("Category", "Emulator")]
public class GetEventsAppliedToAggregateTests()
    : Store.Tests.Features.GetEventsAppliedToAggregateTests(new CosmosDomainServiceFactory());