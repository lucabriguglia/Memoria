using Xunit;

namespace Memoria.EventSourcing.Store.Cosmos.Tests.Features.DomainService;

[Trait("Category", "Emulator")]
public class GetEventsTests() : Store.Tests.Features.GetEventsTests(new CosmosDomainServiceFactory());