namespace OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Tests.Features.DomainService;

public class GetAggregateTests() : OpenCqrs.EventSourcing.Store.Tests.Features.GetAggregateTests(new DomainServiceFactory());
