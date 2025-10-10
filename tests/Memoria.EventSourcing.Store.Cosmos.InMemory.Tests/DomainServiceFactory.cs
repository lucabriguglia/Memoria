using Memoria.EventSourcing.Store.Tests;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Time.Testing;

namespace Memoria.EventSourcing.Store.Cosmos.InMemory.Tests;

public class DomainServiceFactory : IDomainServiceFactory
{
    public IDomainService CreateDomainService(FakeTimeProvider timeProvider, IHttpContextAccessor httpContextAccessor) =>
        new InMemoryCosmosDomainService(new InMemoryCosmosStorage(), timeProvider, httpContextAccessor);
}
