using Memoria.EventSourcing.Store.Cosmos.Configuration;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace Memoria.EventSourcing.Store.Cosmos;

/// <summary>
/// Provides setup and initialization functionality for Cosmos DB database and container.
/// </summary>
public class CosmosSetup(IOptions<CosmosOptions> cosmosOptions, CosmosClientProvider clientProvider)
{
    /// <summary>
    /// Creates the Cosmos DB database and container if they do not already exist, with the indexing
    /// policy the store is built for.
    /// </summary>
    /// <param name="throughput">The throughput to provision for the container. Default is 400 RU/s.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the created or existing container.</returns>
    /// <remarks>
    /// See <see cref="CosmosIndexingPolicy"/> for what the policy does and why. To keep the Cosmos DB
    /// default of indexing everything, pass <c>new IndexingPolicy()</c> to the overload instead.
    /// </remarks>
    public Task<Container> CreateDatabaseAndContainerIfNotExist(int throughput = 400) =>
        CreateDatabaseAndContainerIfNotExist(CosmosIndexingPolicy.CreateRecommended(), throughput);

    /// <summary>
    /// Creates the Cosmos DB database and container if they do not already exist, with a specific
    /// indexing policy.
    /// </summary>
    /// <param name="indexingPolicy">The indexing policy for the container.</param>
    /// <param name="throughput">The throughput to provision for the container. Default is 400 RU/s.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the created or existing container.</returns>
    /// <remarks>
    /// The policy applies only to a container this call creates. An existing container keeps the
    /// policy it already has — see <see cref="ReplaceIndexingPolicy"/>.
    /// </remarks>
    public async Task<Container> CreateDatabaseAndContainerIfNotExist(IndexingPolicy indexingPolicy,
        int throughput = 400)
    {
        var databaseResponse =
            await clientProvider.Client.CreateDatabaseIfNotExistsAsync(cosmosOptions.Value.DatabaseName);

        var containerProperties = new ContainerProperties(cosmosOptions.Value.ContainerName, "/streamId")
        {
            IndexingPolicy = indexingPolicy
        };

        var containerResponse =
            await databaseResponse.Database.CreateContainerIfNotExistsAsync(containerProperties, throughput);

        return containerResponse.Container;
    }

    /// <summary>
    /// Replaces the indexing policy on the existing container.
    /// </summary>
    /// <param name="indexingPolicy">The policy to apply.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The container.</returns>
    /// <remarks>
    /// <para>
    /// Separate from container creation, and deliberately something you have to ask for. Cosmos DB
    /// reindexes in the background: the container stays online and writes keep succeeding, but
    /// <b>queries can return incomplete results until the transformation finishes</b>. On a
    /// container that already holds data, do this during a quiet period and watch
    /// <c>indexTransformationProgress</c>.
    /// </para>
    /// <para>
    /// Applying the same policy twice is a no-op, so this is safe to call from a deployment step.
    /// </para>
    /// </remarks>
    public async Task<Container> ReplaceIndexingPolicy(IndexingPolicy indexingPolicy,
        CancellationToken cancellationToken = default)
    {
        var container = clientProvider.Client.GetContainer(cosmosOptions.Value.DatabaseName,
            cosmosOptions.Value.ContainerName);

        var properties = (await container.ReadContainerAsync(cancellationToken: cancellationToken)).Resource;
        properties.IndexingPolicy = indexingPolicy;

        var response = await container.ReplaceContainerAsync(properties, cancellationToken: cancellationToken);
        return response.Container;
    }
}
