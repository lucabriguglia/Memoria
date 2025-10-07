namespace OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Tests.Features.DomainServiceTests;

public class GetLatestEventSequenceTests() : OpenCqrs.EventSourcing.Store.Tests.Features.GetLatestEventSequenceTests(new DomainServiceFactory());
