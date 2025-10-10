namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Tests.Features.DomainService;

public class GetInMemoryAggregateTests() : Store.Tests.Features.GetInMemoryAggregateTests(new DomainServiceFactory());
