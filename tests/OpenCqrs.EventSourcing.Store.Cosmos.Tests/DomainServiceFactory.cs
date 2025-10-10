using Memoria.EventSourcing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using OpenCqrs.EventSourcing.Store.Cosmos.Configuration;
using OpenCqrs.EventSourcing.Store.Tests;

namespace OpenCqrs.EventSourcing.Store.Cosmos.Tests;

public class DomainServiceFactory : IDomainServiceFactory
{
    public IDomainService CreateDomainService(FakeTimeProvider timeProvider, IHttpContextAccessor httpContextAccessor)
    {
        var cosmosOptions = Substitute.For<IOptions<CosmosOptions>>();
        cosmosOptions.Value.Returns(new CosmosOptions
        {
            Endpoint = "https://localhost:8081",
            AuthKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw=="
        });

        var dataStore = new CosmosDataStore(cosmosOptions, timeProvider, httpContextAccessor);
        var domainService = new CosmosDomainService(cosmosOptions, timeProvider, httpContextAccessor, dataStore);

        var cosmosSetup = new CosmosSetup(cosmosOptions);
        _ = cosmosSetup.CreateDatabaseAndContainerIfNotExist();

        return domainService;
    }
}
