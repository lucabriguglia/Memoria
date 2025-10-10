using Memoria.EventSourcing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Time.Testing;

namespace OpenCqrs.EventSourcing.Store.Tests;

public interface IDomainServiceFactory
{
    IDomainService CreateDomainService(FakeTimeProvider timeProvider, IHttpContextAccessor httpContextAccessor);
}
