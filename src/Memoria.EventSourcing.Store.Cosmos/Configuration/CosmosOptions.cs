using Microsoft.Azure.Cosmos;
using System.ComponentModel.DataAnnotations;

namespace OpenCqrs.EventSourcing.Store.Cosmos.Configuration;

/// <summary>
/// Represents configuration options for connecting to an Azure Cosmos DB instance.
/// </summary>
public class CosmosOptions
{
    /// <summary>
    /// Gets or sets the Cosmos DB endpoint URI.
    /// </summary>
    /// <remarks>
    /// This property specifies the URI of the Cosmos DB instance that will be accessed by the application.
    /// It is a required property and must be a valid URI string pointing to your Cosmos DB service endpoint.
    /// </remarks>
    [Required]
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Cosmos DB authentication key.
    /// </summary>
    /// <remarks>
    /// This property holds the authentication key used to authorize access to a Cosmos DB account.
    /// It is a required property and must be a valid string that corresponds to the key associated with your Cosmos DB account.
    /// </remarks>
    [Required]
    public string AuthKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the Azure Cosmos DB database.
    /// </summary>
    /// <remarks>
    /// This property specifies the logical database name to be used in the Cosmos DB instance.
    /// It is required for defining the database the application will interact with and must be a valid database name.
    /// </remarks>
    public string DatabaseName { get; set; } = "Memoria";

    /// <summary>
    /// Gets or sets the name of the Cosmos DB container.
    /// </summary>
    /// <remarks>
    /// This property specifies the name of the container within the specified database
    /// in the Azure Cosmos DB account. It is used when creating or accessing a specific
    /// container for storing and retrieving data. If not specified, a default container
    /// name may be used.
    /// </remarks>
    public string ContainerName { get; set; } = "Domain";

    /// <summary>
    /// Gets or sets the custom client options for configuring the Cosmos DB client.
    /// </summary>
    /// <remarks>
    /// This property allows for the configuration of advanced client options for the Cosmos DB client,
    /// including settings such as the application name, connection mode, and serializer options.
    /// It uses the <see cref="Microsoft.Azure.Cosmos.CosmosClientOptions"/> class.
    /// </remarks>
    public CosmosClientOptions ClientOptions { get; set; } = new()
    {
        ApplicationName = "Memoria",
        ConnectionMode = ConnectionMode.Direct
    };
}
