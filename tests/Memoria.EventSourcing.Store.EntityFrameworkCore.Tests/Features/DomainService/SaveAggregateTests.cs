namespace OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Tests.Features.DomainService;

public class SaveAggregateTests() : OpenCqrs.EventSourcing.Store.Tests.Features.SaveAggregateTests(new DomainServiceFactory());
