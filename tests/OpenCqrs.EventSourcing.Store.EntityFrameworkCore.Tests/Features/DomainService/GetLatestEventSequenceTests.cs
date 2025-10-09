namespace OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Tests.Features.DomainService;

public class GetLatestEventSequenceTests() : OpenCqrs.EventSourcing.Store.Tests.Features.GetLatestEventSequenceTests(new DomainServiceFactory());
