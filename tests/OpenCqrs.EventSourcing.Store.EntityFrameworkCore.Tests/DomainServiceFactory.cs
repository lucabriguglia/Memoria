using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Tests.Data;
using OpenCqrs.EventSourcing.Store.Tests;

namespace OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Tests;

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
        builder.UseInMemoryDatabase("OpenCQRS");
        return builder.Options;
    }
}
