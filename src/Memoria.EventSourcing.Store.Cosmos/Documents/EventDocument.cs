using Memoria.EventSourcing.Domain;
using Newtonsoft.Json;

namespace Memoria.EventSourcing.Store.Cosmos.Documents;

/// <summary>
/// Represents a document that stores event information in the Cosmos DB Event Sourcing store.
/// This document contains all the necessary data to persist and reconstruct domain events.
/// </summary>
public class EventDocument
{
    /// <summary>
    /// Gets or sets the identifier of the event stream to which this event belongs.
    /// This is used to group related events together in the same stream.
    /// </summary>
    [JsonProperty("streamId")]
    public string StreamId { get; set; } = null!;

    /// <summary>
    /// Gets the type of the document, which identifies this document as an event document.
    /// This is used to differentiate between various document types within the Cosmos DB Event Sourcing store.
    /// </summary>
    [JsonProperty("documentType")]
    public static string DocumentType => Documents.DocumentType.Event;

    /// <summary>
    /// Gets or sets the type name of the event.
    /// This is used to identify the specific type of event for deserialization purposes.
    /// </summary>
    [JsonProperty("eventType")]
    public string EventType { get; set; } = null!;

    /// <summary>
    /// Gets or sets the unique identifier of the event document.
    /// This identifier is used to uniquely distinguish events within the Cosmos DB Event Sourcing store.
    /// </summary>
    [JsonProperty("id")]
    public string Id { get; set; } = null!;

    /// <summary>
    /// Gets or sets the sequence number of the event within the event stream.
    /// This is used to maintain the chronological order of events within the same stream.
    /// </summary>
    [JsonProperty("sequence")]
    public int Sequence { get; set; }

    /// <summary>
    /// Gets or sets the serialized data of the event.
    /// This contains the JSON representation of the event's properties and state.
    /// </summary>
    [JsonProperty("data")]
    public string Data { get; set; } = null!;

    /// <summary>
    /// Gets or sets the date and time when the event was created.
    /// This timestamp is used for auditing and chronological ordering purposes.
    /// </summary>
    [JsonProperty("createdDate")]
    public DateTimeOffset CreatedDate { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user or system that created the event.
    /// This property is optional and is used for auditing purposes.
    /// </summary>
    [JsonProperty("createdBy")]
    public string? CreatedBy { get; set; }
}

/// <summary>
/// Provides extension methods for the <see cref="EventDocument"/> class.
/// These methods facilitate the conversion between event documents and domain events.
/// </summary>
public static class EventDocumentExtensions
{
    private static readonly JsonSerializerSettings JsonSerializerSettings = new()
    {
        ContractResolver = new PrivateSetterContractResolver()
    };

    /// <summary>
    /// Converts an <see cref="EventDocument"/> to its corresponding <see cref="IEvent"/> instance.
    /// This method deserializes the event data using the event type information stored in the document.
    /// </summary>
    /// <param name="eventDocument">The event document to convert.</param>
    /// <returns>The deserialized event instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the event type is not found in TypeBindings.</exception>
    public static IEvent ToDomainEvent(this EventDocument eventDocument)
    {
        var typeFound = TypeBindings.EventTypeBindings.TryGetValue(eventDocument.EventType, out var eventType);
        if (typeFound is false)
        {
            throw new InvalidOperationException($"Event type {eventDocument.EventType} not found in TypeBindings");
        }

        return (IEvent)JsonConvert.DeserializeObject(eventDocument.Data, eventType!, JsonSerializerSettings)!;
    }
}
