namespace Memoria.EventSourcing.Store.Cosmos.Tests.Features.DomainService;

public class GetLatestEventSequenceTests() : Store.Tests.Features.GetLatestEventSequenceTests(new DomainServiceFactory());
