using System.Reflection;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;
using Newtonsoft.Json;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Extensions;

/// <summary>
/// Provides extension methods for converting domain events to Entity Framework Core entities
/// for persistence in the event sourcing store. These extensions handle serialization, metadata
/// extraction, and proper entity construction for event storage.
/// </summary>
public static class EventExtensions
{
    /// <summary>
    /// Converts an event to its corresponding <see cref="EventEntity"/> for database persistence.
    /// This method handles serialization, metadata extraction, and infrastructure property mapping to create
    /// a complete database entity ready for storage in the event sourcing system.
    /// </summary>
    /// <param name="event">
    /// The event instance to convert. Must implement <see cref="IEvent"/> and have
    /// the <see cref="EventType"/> attribute for proper type metadata extraction.
    /// </param>
    /// <param name="streamId">
    /// The stream identifier that associates this event with its owning aggregate's event stream.
    /// Provides the logical grouping mechanism for related events in the event store.
    /// </param>
    /// <param name="sequence">
    /// The sequence number for this event within its stream. Must be unique within the stream
    /// and typically increments sequentially to maintain proper event ordering.
    /// </param>
    /// <returns>
    /// A fully configured <see cref="EventEntity"/> containing the serialized event data,
    /// type metadata, stream association, and infrastructure properties needed for persistence.
    /// The entity includes a new unique identifier and is ready for immediate database storage.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the event type does not have the required <see cref="EventType"/> attribute.
    /// This attribute is essential for type metadata extraction and proper event store operation.
    /// </exception>
    /// <exception cref="JsonSerializationException">
    /// Thrown when the event cannot be serialized to JSON, typically due to circular references,
    /// unsupported data types, or serialization configuration issues.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any of the required parameters (@event, streamId) are null.
    /// </exception>
    /// <example>
    /// <code>
    /// // Basic usage in event store implementation
    /// public async Task SaveEventsAsync(string streamId, IEnumerable&lt;IDomainEvent&gt; events)
    /// {
    ///     var streamIdObj = new StreamId(streamId);
    ///     var eventEntities = events.Select((evt, index) =&gt;
    ///     {
    ///         var entity = evt.ToEventEntity(streamIdObj, _currentSequence + index);
    ///         
    ///         // Set audit information
    ///         entity.CreatedDate = DateTimeOffset.UtcNow;
    ///         entity.CreatedBy = _currentUser.Id;
    ///         
    ///         return entity;
    ///     }).ToList();
    ///     
    ///     _context.Events.AddRange(eventEntities);
    ///     await _context.SaveChangesAsync();
    ///     
    ///     _currentSequence += events.Count();
    /// }
    /// 
    /// // Usage with error handling
    /// public EventEntity ConvertDomainEvent(IDomainEvent @event, IStreamId streamId, int sequence)
    /// {
    ///     try
    ///     {
    ///         var entity = @event.ToEventEntity(streamId, sequence);
    ///         
    ///         // Verify the conversion was successful
    ///         Debug.Assert(!string.IsNullOrEmpty(entity.Id));
    ///         Debug.Assert(entity.StreamId == streamId.Id);
    ///         Debug.Assert(entity.Sequence == sequence);
    ///         Debug.Assert(!string.IsNullOrEmpty(entity.Data));
    ///         
    ///         return entity;
    ///     }
    ///     catch (InvalidOperationException ex)
    ///     {
    ///         _logger.LogError("Event {EventType} missing required EventType attribute", 
    ///             @event.GetType().Name);
    ///         throw;
    ///     }
    ///     catch (JsonException ex)
    ///     {
    ///         _logger.LogError("Failed to serialize event {EventType}: {Error}", 
    ///             @event.GetType().Name, ex.Message);
    ///         throw;
    ///     }
    /// }
    /// 
    /// // Example with specific event types
    /// [EventType("OrderPlaced", 1)]
    /// public record OrderPlacedEvent : IDomainEvent
    /// {
    ///     public Guid OrderId { get; init; }
    ///     public Guid CustomerId { get; init; }
    ///     public decimal TotalAmount { get; init; }
    ///     public DateTime PlacedAt { get; init; }
    ///     public List&lt;OrderItem&gt; Items { get; init; } = new();
    /// }
    /// 
    /// // Converting and saving the event
    /// var orderPlacedEvent = new OrderPlacedEvent
    /// {
    ///     OrderId = orderId,
    ///     CustomerId = customerId,
    ///     TotalAmount = 199.99m,
    ///     PlacedAt = DateTime.UtcNow,
    ///     Items = orderItems
    /// };
    /// 
    /// var streamId = new OrderStreamId(orderId);
    /// var entity = orderPlacedEvent.ToEventEntity(streamId, 1);
    /// 
    /// // The entity will have:
    /// // - Id: New GUID string (e.g., "a1b2c3d4-e5f6-7890-abcd-ef1234567890")
    /// // - StreamId: streamId.Id (e.g., "order-{orderId}")
    /// // - Sequence: 1
    /// // - TypeName: "OrderPlaced"
    /// // - TypeVersion: 1
    /// // - Data: JSON serialized OrderPlacedEvent data
    /// 
    /// // Batch processing example
    /// public async Task SaveEventBatchAsync(string streamId, List&lt;IDomainEvent&gt; events)
    /// {
    ///     var streamIdObj = new StreamId(streamId);
    ///     var startSequence = await GetNextSequenceNumberAsync(streamId);
    ///     
    ///     var entities = new List&lt;EventEntity&gt;();
    ///     var currentTime = DateTimeOffset.UtcNow;
    ///     var userId = _currentUser.Id;
    ///     
    ///     for (int i = 0; i &lt; events.Count; i++)
    ///     {
    ///         var entity = events[i].ToEventEntity(streamIdObj, startSequence + i);
    ///         entity.CreatedDate = currentTime;
    ///         entity.CreatedBy = userId;
    ///         
    ///         entities.Add(entity);
    ///     }
    ///     
    ///     _context.Events.AddRange(entities);
    ///     await _context.SaveChangesAsync();
    /// }
    /// 
    /// // Error resilience example
    /// public List&lt;EventEntity&gt; ConvertEventsWithErrorHandling(
    ///     List&lt;IDomainEvent&gt; events, 
    ///     IStreamId streamId, 
    ///     int startSequence)
    /// {
    ///     var entities = new List&lt;EventEntity&gt;();
    ///     var errors = new List&lt;string&gt;();
    ///     
    ///     for (int i = 0; i &lt; events.Count; i++)
    ///     {
    ///         try
    ///         {
    ///             var entity = events[i].ToEventEntity(streamId, startSequence + i);
    ///             entities.Add(entity);
    ///         }
    ///         catch (Exception ex)
    ///         {
    ///             errors.Add($"Event {i} ({events[i].GetType().Name}): {ex.Message}");
    ///         }
    ///     }
    ///     
    ///     if (errors.Any())
    ///     {
    ///         _logger.LogError("Failed to convert {Count} events: {Errors}", 
    ///             errors.Count, string.Join("; ", errors));
    ///         
    ///         // Decide whether to throw or continue with partial results
    ///         throw new AggregateException($"Event conversion failures: {string.Join("; ", errors)}");
    ///     }
    ///     
    ///     return entities;
    /// }
    /// </code>
    /// </example>
    public static EventEntity ToEventEntity(this IEvent @event, IStreamId streamId, int sequence)
    {
        var eventType = @event.GetType().GetCustomAttribute<EventType>();
        if (eventType == null)
        {
            throw new InvalidOperationException($"Event {@event.GetType().Name} does not have a EventType attribute.");
        }

        return new EventEntity
        {
            Id = $"{streamId.Id}:{sequence}",
            StreamId = streamId.Id,
            Sequence = sequence,
            EventType = TypeBindings.GetTypeBindingKey(eventType.Name, eventType.Version),
            Data = JsonConvert.SerializeObject(@event)
        };
    }
}
