namespace OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Tests.Features.DomainService;

public class GetInMemoryAggregateTests() : OpenCqrs.EventSourcing.Store.Tests.Features.GetInMemoryAggregateTests(new DomainServiceFactory());
