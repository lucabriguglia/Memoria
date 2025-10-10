using Memoria.EventSourcing;
using Memoria.EventSourcing.Domain;
using Newtonsoft.Json;

// ReSharper disable EntityFramework.ModelValidation.UnlimitedStringLength

namespace OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Entities;

/// <summary>
/// Represents the database entity for storing domain events.
/// </summary>
public class EventEntity : IAuditableEntity
{
    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    public string Id { get; set; } = null!;

    /// <summary>
    /// Gets or sets the stream ID.
    /// </summary>
    public string StreamId { get; set; } = null!;

    /// <summary>
    /// Gets or sets the event type.
    /// </summary>
    public string EventType { get; set; } = null!;

    /// <summary>
    /// Gets or sets the sequence number.
    /// </summary>
    public int Sequence { get; set; }

    /// <summary>
    /// Gets or sets the JSON data.
    /// </summary>
    public string Data { get; set; } = null!;

    /// <summary>
    /// Gets or sets the created date.
    /// </summary>
    public DateTimeOffset CreatedDate { get; set; }

    /// <summary>
    /// Gets or sets the created by.
    /// </summary>
    public string? CreatedBy { get; set; }
}

/// <summary>
/// Extension methods for EventEntity.
/// </summary>
public static class EventEntityExtensions
{
    /// <summary>
    /// JSON serializer settings.
    /// </summary>
    private static readonly JsonSerializerSettings JsonSerializerSettings = new()
    {
        ContractResolver = new PrivateSetterContractResolver()
    };

    /// <summary>
    /// Converts an EventEntity to a event.
    /// </summary>
    /// <param name="eventEntity">The entity.</param>
    /// <returns>The event.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the event type is not found.</exception>
    public static IEvent ToDomainEvent(this EventEntity eventEntity)
    {
        var typeFound = TypeBindings.EventTypeBindings.TryGetValue(eventEntity.EventType, out var eventType);
        if (typeFound is false)
        {
            throw new InvalidOperationException($"Event type {eventEntity.EventType} not found in TypeBindings");
        }

        return (IEvent)JsonConvert.DeserializeObject(eventEntity.Data, eventType!, JsonSerializerSettings)!;
    }
}
