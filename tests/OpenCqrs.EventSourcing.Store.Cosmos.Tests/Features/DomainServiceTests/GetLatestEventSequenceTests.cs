namespace OpenCqrs.EventSourcing.Store.Cosmos.Tests.Features.DomainServiceTests;

public class GetLatestEventSequenceTests() : OpenCqrs.EventSourcing.Store.Tests.Features.GetLatestEventSequenceTests(new DomainServiceFactory());
