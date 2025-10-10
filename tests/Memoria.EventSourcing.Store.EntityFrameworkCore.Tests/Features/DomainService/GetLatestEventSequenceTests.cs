namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Tests.Features.DomainService;

public class GetLatestEventSequenceTests() : Store.Tests.Features.GetLatestEventSequenceTests(new DomainServiceFactory());
