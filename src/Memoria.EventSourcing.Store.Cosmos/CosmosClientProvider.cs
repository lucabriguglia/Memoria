using Memoria.EventSourcing.Store.Cosmos.Configuration;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace Memoria.EventSourcing.Store.Cosmos;

/// <summary>
/// Owns the single <see cref="CosmosClient"/> the store uses, and the <see cref="Container"/>
/// resolved from <see cref="CosmosOptions"/>.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="CosmosClient"/> is meant to live for the lifetime of the application. Each instance
/// performs its own account discovery, builds its own routing map and — in
/// <see cref="ConnectionMode.Direct"/>, the Memoria default — opens its own connections to every
/// replica it touches. None of that is shared between instances, so creating one per request pays
/// the warm-up cost again every time and disposing it throws the connections away.
/// </para>
/// <para>
/// <c>AddMemoriaCosmos</c> registers this type as a singleton, which is what makes the client a
/// singleton. Because the client is built once, changes to <see cref="CosmosOptions"/> after the
/// first resolution have no effect on the connection it holds.
/// </para>
/// </remarks>
public sealed class CosmosClientProvider : IDisposable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosClientProvider"/> class.
    /// </summary>
    /// <param name="options">The Cosmos DB configuration options.</param>
    public CosmosClientProvider(IOptions<CosmosOptions> options)
    {
        var cosmosOptions = options.Value;

        Client = new CosmosClient(cosmosOptions.Endpoint, cosmosOptions.AuthKey, cosmosOptions.ClientOptions);
        Container = Client.GetContainer(cosmosOptions.DatabaseName, cosmosOptions.ContainerName);
    }

    /// <summary>
    /// Gets the shared Cosmos DB client. Do not dispose it: it belongs to this provider, which the
    /// container disposes when the application shuts down.
    /// </summary>
    public CosmosClient Client { get; }

    /// <summary>
    /// Gets the container holding events, aggregates, aggregate-event links and projections.
    /// </summary>
    public Container Container { get; }

    /// <summary>
    /// Disposes the shared Cosmos DB client. Called by the dependency injection container when the
    /// application shuts down.
    /// </summary>
    public void Dispose() => Client.Dispose();
}
