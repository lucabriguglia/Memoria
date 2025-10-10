namespace OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Tests.Features.DomainService;

public class UpdateAggregateTests() : OpenCqrs.EventSourcing.Store.Tests.Features.UpdateAggregateTests(new DomainServiceFactory());
