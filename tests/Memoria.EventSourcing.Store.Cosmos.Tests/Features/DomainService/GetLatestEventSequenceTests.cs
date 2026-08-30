using Xunit;

namespace Memoria.EventSourcing.Store.Cosmos.Tests.Features.DomainService;

[Trait("Category", "Emulator")]
public class GetLatestEventSequenceTests()
    : Store.Tests.Features.GetLatestEventSequenceTests(new CosmosDomainServiceFactory());