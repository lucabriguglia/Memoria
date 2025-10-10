using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using OpenCqrs.EventSourcing.Store.Cosmos.Configuration;

namespace OpenCqrs.EventSourcing.Store.Cosmos;

/// <summary>
/// Provides setup and initialization functionality for Cosmos DB database and container.
/// </summary>
public class CosmosSetup(IOptions<CosmosOptions> cosmosOptions)
{
    /// <summary>
    /// Creates the Cosmos DB database and container if they do not already exist.
    /// </summary>
    /// <param name="throughput">The throughput to provision for the container. Default is 400 RU/s.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the created or existing container.</returns>
    public async Task<Container> CreateDatabaseAndContainerIfNotExist(int throughput = 400)
    {
        var cosmosClient = new CosmosClient(cosmosOptions.Value.Endpoint, cosmosOptions.Value.AuthKey, cosmosOptions.Value.ClientOptions);
        var databaseResponse = await cosmosClient.CreateDatabaseIfNotExistsAsync(cosmosOptions.Value.DatabaseName);
        var containerResponse = await databaseResponse.Database.CreateContainerIfNotExistsAsync(cosmosOptions.Value.ContainerName, "/streamId", throughput);
        return containerResponse.Container;
    }
}
