using Memoria.EventSourcing.Store.Cosmos.Configuration;
using Memoria.EventSourcing.Store.Tests;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace Memoria.EventSourcing.Store.Cosmos.Tests;

public class CosmosDomainServiceFactory : IDomainServiceFactory
{
    public IDomainService CreateDomainService(FakeTimeProvider timeProvider, IHttpContextAccessor httpContextAccessor)
    {
        var cosmosOptions = Substitute.For<IOptions<CosmosOptions>>();
        cosmosOptions.Value.Returns(new CosmosOptions
        {
            Endpoint = "https://localhost:8081",
            AuthKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw=="
        });

        var clientProvider = new CosmosClientProvider(cosmosOptions);
        var dataStore = new CosmosDataStore(clientProvider, timeProvider, httpContextAccessor);
        var domainService = new CosmosDomainService(clientProvider, timeProvider, httpContextAccessor, dataStore);

        var cosmosSetup = new CosmosSetup(cosmosOptions, clientProvider);
        _ = cosmosSetup.CreateDatabaseAndContainerIfNotExist();

        return domainService;
    }
}