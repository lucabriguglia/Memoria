using Memoria.EventSourcing;
using Memoria.EventSourcing.Store.Cosmos.InMemory;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Time.Testing;
using OpenCqrs.EventSourcing.Store.Tests;

namespace OpenCqrs.EventSourcing.Store.Cosmos.InMemory.Tests;

public class DomainServiceFactory : IDomainServiceFactory
{
    public IDomainService CreateDomainService(FakeTimeProvider timeProvider, IHttpContextAccessor httpContextAccessor) =>
        new InMemoryCosmosDomainService(new InMemoryCosmosStorage(), timeProvider, httpContextAccessor);
}
