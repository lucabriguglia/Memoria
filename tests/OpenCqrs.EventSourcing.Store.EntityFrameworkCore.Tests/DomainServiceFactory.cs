using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Time.Testing;
using OpenCqrs.EventSourcing.Store.Tests;

namespace OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Tests;

public class DomainServiceFactory : IDomainServiceFactory
{
    public IDomainService CreateDomainService(FakeTimeProvider timeProvider, IHttpContextAccessor httpContextAccessor)
    {
        var dbContext = Shared.CreateTestDbContext();
        return new EntityFrameworkCoreDomainService(dbContext);
    }
}
