namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Tests.Features.DomainService;

public class AggregateFoldedDiagnosticsTests()
    : Store.Tests.Features.AggregateFoldedDiagnosticsTests(new DomainServiceFactory());
