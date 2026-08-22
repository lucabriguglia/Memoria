namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Tests.Features.DomainService;

public class GetProjectionTests() : Store.Tests.Features.GetProjectionTests(new DomainServiceFactory());
