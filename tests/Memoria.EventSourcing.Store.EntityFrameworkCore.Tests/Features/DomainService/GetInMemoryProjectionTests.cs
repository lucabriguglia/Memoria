namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Tests.Features.DomainService;

public class GetInMemoryProjectionTests() : Store.Tests.Features.GetInMemoryProjectionTests(new DomainServiceFactory());
