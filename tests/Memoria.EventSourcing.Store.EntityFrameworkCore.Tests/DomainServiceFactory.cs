using Memoria.EventSourcing.Store.EntityFrameworkCore.Tests.Data;
using Memoria.EventSourcing.Store.Tests;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Tests;

public class DomainServiceFactory : IDomainServiceFactory
{
    public IDomainService CreateDomainService(FakeTimeProvider timeProvider, IHttpContextAccessor httpContextAccessor)
    {
        var dbContext = new TestDbContext(CreateContextOptions(), timeProvider, httpContextAccessor);
        return new EntityFrameworkCoreDomainService(dbContext);
    }

    private static DbContextOptions<DomainDbContext> CreateContextOptions()
    {
        var builder = new DbContextOptionsBuilder<DomainDbContext>();
        builder.UseInMemoryDatabase("Memoria");
        return builder.Options;
    }
}
