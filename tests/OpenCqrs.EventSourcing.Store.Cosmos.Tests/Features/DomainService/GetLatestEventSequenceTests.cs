namespace OpenCqrs.EventSourcing.Store.Cosmos.Tests.Features.DomainService;

public class GetLatestEventSequenceTests() : OpenCqrs.EventSourcing.Store.Tests.Features.GetLatestEventSequenceTests(new DomainServiceFactory());
