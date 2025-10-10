namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Tests.Features.DomainService;

public class UpdateAggregateTests() : Store.Tests.Features.UpdateAggregateTests(new DomainServiceFactory());
