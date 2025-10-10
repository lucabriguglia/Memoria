using System.Reflection;
using Memoria.EventSourcing.Domain;
using Newtonsoft.Json;
using OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Entities;

namespace OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Extensions;

/// <summary>
/// Provides extension methods for converting domain aggregates to Entity Framework Core entities
/// for persistence in the event sourcing store. These extensions handle serialization, metadata
/// extraction, and proper entity construction for database storage.
/// </summary>
public static class AggregateExtensions
{
    /// <summary>
    /// Converts a domain aggregate to its corresponding <see cref="AggregateEntity"/> for database persistence.
    /// This method handles serialization, metadata extraction, and infrastructure property mapping to create
    /// a complete database entity ready for storage in the event sourcing system.
    /// </summary>
    /// <param name="aggregate">
    /// The domain aggregate instance to convert. Must implement <see cref="IAggregateRoot"/> and have
    /// the <see cref="AggregateType"/> attribute for proper type metadata extraction.
    /// </param>
    /// <param name="streamId">
    /// The stream identifier that links this aggregate to its event stream. Provides the relationship
    /// between the aggregate snapshot and its corresponding events.
    /// </param>
    /// <param name="aggregateId">
    /// The unique identifier for this aggregate instance. Used to create versioned identifiers
    /// that support aggregate type evolution and disambiguation.
    /// </param>
    /// <param name="newLatestEventSequence">
    /// The sequence number of the most recent event that was applied to create this aggregate state.
    /// Used for consistency verification and determining when snapshots are current.
    /// </param>
    /// <returns>
    /// A fully configured <see cref="AggregateEntity"/> containing the serialized aggregate data,
    /// type metadata, versioning information, and infrastructure properties needed for persistence.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the aggregate type does not have the required <see cref="AggregateType"/> attribute.
    /// This attribute is essential for type metadata extraction and proper serialization support.
    /// </exception>
    /// <exception cref="JsonSerializationException">
    /// Thrown when the aggregate cannot be serialized to JSON, typically due to circular references,
    /// unsupported types, or serialization configuration issues.
    /// </exception>
    /// <example>
    /// <code>
    /// // Basic usage in aggregate repository
    /// public async Task SaveAggregateAsync&lt;T&gt;(T aggregate, IStreamId streamId, IAggregateId aggregateId, int eventSequence) 
    ///     where T : IAggregate
    /// {
    ///     var entity = aggregate.ToAggregateEntity(streamId, aggregateId, eventSequence);
    ///     
    ///     // Set audit information
    ///     entity.CreatedDate = DateTimeOffset.UtcNow;
    ///     entity.UpdatedDate = DateTimeOffset.UtcNow;
    ///     entity.CreatedBy = _currentUser.Id;
    ///     entity.UpdatedBy = _currentUser.Id;
    ///     
    ///     _context.Aggregates.Add(entity);
    ///     await _context.SaveChangesAsync();
    /// }
    /// 
    /// // Usage with error handling
    /// public AggregateEntity ConvertAggregate(IAggregate aggregate, IStreamId streamId, IAggregateId aggregateId, int sequence)
    /// {
    ///     try
    ///     {
    ///         var entity = aggregate.ToAggregateEntity(streamId, aggregateId, sequence);
    ///         
    ///         // Verify the conversion was successful
    ///         Debug.Assert(entity.StreamId == streamId.Id);
    ///         Debug.Assert(entity.Version == aggregate.Version);
    ///         Debug.Assert(!string.IsNullOrEmpty(entity.Data));
    ///         
    ///         return entity;
    ///     }
    ///     catch (InvalidOperationException ex)
    ///     {
    ///         _logger.LogError("Aggregate {AggregateType} missing required AggregateType attribute", 
    ///             aggregate.GetType().Name);
    ///         throw;
    ///     }
    ///     catch (JsonException ex)
    ///     {
    ///         _logger.LogError("Failed to serialize aggregate {AggregateId}: {Error}", 
    ///             aggregateId.Id, ex.Message);
    ///         throw;
    ///     }
    /// }
    /// 
    /// // Example with specific aggregate types
    /// [AggregateType("Order", 1)]
    /// public class OrderAggregate : AggregateRoot
    /// {
    ///     public Guid OrderId { get; private set; }
    ///     public string CustomerName { get; private set; }
    ///     // ... other properties
    /// }
    /// 
    /// // Converting and saving the aggregate
    /// var orderAggregate = new OrderAggregate(orderId, customerName);
    /// var streamId = new OrderStreamId(orderId);
    /// var aggregateId = new OrderAggregateId(orderId);
    /// 
    /// var entity = orderAggregate.ToAggregateEntity(streamId, aggregateId, 0);
    /// 
    /// // The entity will have:
    /// // - Id: "{orderId}:1" (versioned identifier)
    /// // - StreamId: streamId.Id
    /// // - TypeName: "Order"
    /// // - TypeVersion: 1
    /// // - Data: JSON serialized OrderAggregate business state
    /// // - Version: orderAggregate.Version
    /// // - LatestEventSequence: 0
    /// </code>
    /// </example>
    public static AggregateEntity ToAggregateEntity<T>(this IAggregateRoot aggregate, IStreamId streamId, IAggregateId<T> aggregateId, int newLatestEventSequence) where T : IAggregateRoot
    {
        var aggregateType = aggregate.GetType().GetCustomAttribute<AggregateType>();
        if (aggregateType == null)
        {
            throw new InvalidOperationException($"Aggregate {aggregate.GetType().Name} does not have a AggregateType attribute.");
        }

        aggregate.StreamId = streamId.Id;
        aggregate.AggregateId = aggregateId.ToStoreId();
        aggregate.LatestEventSequence = newLatestEventSequence;

        return new AggregateEntity
        {
            Id = aggregateId.ToStoreId(),
            StreamId = streamId.Id,
            Version = aggregate.Version,
            LatestEventSequence = newLatestEventSequence,
            AggregateType = TypeBindings.GetTypeBindingKey(aggregateType.Name, aggregateType.Version),
            Data = JsonConvert.SerializeObject(aggregate)
        };
    }
}
