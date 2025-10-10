using System.Reflection;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.Cosmos.Documents;
using Newtonsoft.Json;

namespace Memoria.EventSourcing.Store.Cosmos.Extensions;

/// <summary>
/// Provides extension methods for converting domain events to documents suitable for storage in Cosmos DB.
/// These methods facilitate the transformation between domain events and their document representations.
/// </summary>
public static class EventExtensions
{
    /// <summary>
    /// Converts an event to an event document for storage in Cosmos DB.
    /// This method extracts event metadata, generates a unique identifier, and serializes the event data.
    /// </summary>
    /// <param name="event">The event to convert to a document.</param>
    /// <param name="streamId">The stream identifier associated with the event.</param>
    /// <param name="sequence">The sequence number of the event within the stream.</param>
    /// <returns>An <see cref="EventDocument"/> containing the serialized event data and metadata.</returns>
    /// <exception cref="Exception">Thrown when the event type does not have an EventType attribute.</exception>
    public static EventDocument ToEventDocument(this IEvent @event, IStreamId streamId, int sequence)
    {
        var eventType = @event.GetType().GetCustomAttribute<EventType>();
        if (eventType == null)
        {
            throw new InvalidOperationException($"Event {@event.GetType().Name} does not have a EventType attribute.");
        }

        return new EventDocument
        {
            Id = $"{streamId.Id}:{sequence}",
            StreamId = streamId.Id,
            Sequence = sequence,
            EventType = TypeBindings.GetTypeBindingKey(eventType.Name, eventType.Version),
            Data = JsonConvert.SerializeObject(@event)
        };
    }
}
