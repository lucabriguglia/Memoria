using Newtonsoft.Json;

namespace OpenCqrs.EventSourcing.Store.Cosmos.Documents;

/// <summary>
/// Represents a document that stores information about an aggregate event in the Cosmos DB Event Sourcing store.
/// </summary>
public class AggregateEventDocument
{
    /// <summary>
    /// Gets or sets the identifier of the event stream associated with the aggregate event.
    /// This is used to group related events within the same stream for an aggregate.
    /// </summary>
    [JsonProperty("streamId")]
    public string StreamId { get; set; } = null!;

    /// <summary>
    /// Gets the type of the document, which identifies the document as representing an aggregate event.
    /// This is used to differentiate between various document types within the Cosmos DB Event Sourcing store.
    /// </summary>
    [JsonProperty("documentType")]
    public static string DocumentType => Documents.DocumentType.AggregateEvent;

    /// <summary>
    /// Gets or sets the unique identifier of the aggregate event document.
    /// This is a combination of the aggregate identifier, event identifier, and version,
    /// used to uniquely distinguish events within the context of the Cosmos DB Event Sourcing store.
    /// </summary>
    [JsonProperty("id")]
    public string Id { get; set; } = null!;

    /// <summary>
    /// Gets or sets the unique identifier of the aggregate that this event belongs to.
    /// This identifier is used to associate events with their corresponding aggregate root.
    /// </summary>
    [JsonProperty("aggregateId")]
    public string AggregateId { get; set; } = null!;

    /// <summary>
    /// Gets or sets the unique identifier of the event.
    /// This identifier is used to distinguish individual events within the event stream.
    /// </summary>
    [JsonProperty("eventId")]
    public string EventId { get; set; } = null!;

    /// <summary>
    /// Gets or sets the date and time when the event was applied to the aggregate.
    /// This timestamp is used for chronological ordering and auditing purposes.
    /// </summary>
    [JsonProperty("appliedDate")]
    public DateTimeOffset AppliedDate { get; set; }
}
