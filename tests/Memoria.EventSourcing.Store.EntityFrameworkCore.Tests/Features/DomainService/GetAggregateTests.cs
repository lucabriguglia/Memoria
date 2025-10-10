namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Tests.Features.DomainService;

public class GetAggregateTests() : Store.Tests.Features.GetAggregateTests(new DomainServiceFactory());
