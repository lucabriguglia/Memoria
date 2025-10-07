namespace OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Tests.Features.DomainServiceTests;

public class UpdateAggregateTests() : OpenCqrs.EventSourcing.Store.Tests.Features.UpdateAggregateTests(new DomainServiceFactory());
