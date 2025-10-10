namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Tests.Features.DomainService;

public class SaveAggregateTests() : Store.Tests.Features.SaveAggregateTests(new DomainServiceFactory());
