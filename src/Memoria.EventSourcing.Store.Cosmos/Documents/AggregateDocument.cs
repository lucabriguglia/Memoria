using Memoria.EventSourcing.Domain;
using Newtonsoft.Json;

namespace Memoria.EventSourcing.Store.Cosmos.Documents;

/// <summary>
/// Represents a document that stores aggregate data for event sourcing in a Cosmos DB implementation.
/// The document contains metadata and serialized data related to the aggregate.
/// </summary>
public class AggregateDocument
{
    /// <summary>
    /// Gets or sets the unique identifier of the stream associated with the aggregate document.
    /// The stream ID is used to group events that belong to the same aggregate instance
    /// in the context of event sourcing.
    /// </summary>
    [JsonProperty("streamId")]
    public string StreamId { get; set; } = null!;

    /// <summary>
    /// Gets the document type associated with the aggregate document.
    /// This property indicates the type of document being handled,
    /// which is critical for distinguishing between different types of stored entities
    /// in an event-sourcing implementation (e.g., Event, Aggregate, or AggregateEvent).
    /// </summary>
    [JsonProperty("documentType")]
    public static string DocumentType => Documents.DocumentType.Aggregate;

    /// <summary>
    /// Gets or sets the type of the aggregate, typically represented in a "Name:Version" format.
    /// This property is utilized to distinguish between different aggregate types and their versions,
    /// supporting scenarios where versioning or polymorphism is required.
    /// </summary>
    [JsonProperty("aggregateType")]
    public string AggregateType { get; set; } = null!;

    /// <summary>
    /// Gets or sets the unique identifier of the aggregate document.
    /// This identifier is used to correlate and uniquely distinguish documents in the context of event sourcing.
    /// </summary>
    [JsonProperty("id")]
    public string Id { get; set; } = null!;

    /// <summary>
    /// Gets or sets the version of the aggregate document.
    /// The version represents the number of persisted changes to the aggregate,
    /// incrementing with each modification to maintain consistency in event sourcing.
    /// </summary>
    [JsonProperty("version")]
    public int Version { get; set; }

    /// <summary>
    /// Gets or sets the sequence number of the most recent event applied to the aggregate.
    /// This property is useful for tracking the latest state of the aggregate in event-sourcing systems.
    /// </summary>
    [JsonProperty("latestEventSequence")]
    public int LatestEventSequence { get; set; }

    /// <summary>
    /// Gets or sets the serialized representation of the aggregate's state.
    /// This data is used for rehydrating the aggregate when loading aggregate documents
    /// from the Cosmos DB store.
    /// </summary>
    [JsonProperty("data")]
    public string Data { get; set; } = null!;

    /// <summary>
    /// Gets or sets the date and time when the aggregate document was initially created.
    /// This property is used to track the creation timestamp of the aggregate document
    /// for auditing and historical purposes.
    /// </summary>
    [JsonProperty("createdDate")]
    public DateTimeOffset CreatedDate { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user or process that originally created the aggregate document.
    /// This property is used to track the creator of the document for auditing or metadata purposes.
    /// </summary>
    [JsonProperty("createdBy")]
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the most recent update made to the aggregate document.
    /// This property is used to track when the document was last modified, ensuring accurate
    /// auditing and version control within the event sourcing system.
    /// </summary>
    [JsonProperty("updatedDate")]
    public DateTimeOffset UpdatedDate { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user who last modified the aggregate document.
    /// This property is typically used for tracking purposes to audit changes made
    /// to the document within the context of event sourcing and persistence.
    /// </summary>
    [JsonProperty("updatedBy")]
    public string? UpdatedBy { get; set; }
}

public static class AggregateDocumentExtensions
{
    private static readonly JsonSerializerSettings JsonSerializerSettings = new()
    {
        ContractResolver = new PrivateSetterContractResolver()
    };

    /// <summary>
    /// Converts an <see cref="AggregateDocument"/> to an aggregate of a specified type.
    /// </summary>
    /// <typeparam name="T">The type of aggregate to which the document is converted. Must implement <see cref="IAggregateRoot"/>.</typeparam>
    /// <param name="aggregateDocument">The <see cref="AggregateDocument"/> to be converted.</param>
    /// <returns>The aggregate of type <typeparamref name="T"/> populated with data from the <see cref="AggregateDocument"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the aggregate type specified in the <see cref="AggregateDocument"/> is not found in the type bindings.
    /// </exception>
    public static T ToAggregate<T>(this AggregateDocument aggregateDocument) where T : IAggregateRoot
    {
        var typeFound = TypeBindings.AggregateTypeBindings.TryGetValue(aggregateDocument.AggregateType, out var aggregateType);
        if (typeFound is false)
        {
            throw new InvalidOperationException($"Aggregate type {aggregateDocument.AggregateType} not found in TypeBindings");
        }

        var aggregate = (T)JsonConvert.DeserializeObject(aggregateDocument.Data, aggregateType!, JsonSerializerSettings)!;
        aggregate.StreamId = aggregateDocument.StreamId;
        aggregate.AggregateId = aggregateDocument.Id;
        aggregate.Version = aggregateDocument.Version;
        aggregate.LatestEventSequence = aggregateDocument.LatestEventSequence;
        return aggregate;
    }
}
