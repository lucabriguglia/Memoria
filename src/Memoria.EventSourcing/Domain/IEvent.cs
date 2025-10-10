namespace Memoria.EventSourcing.Domain;

/// <summary>
/// Marker interface that identifies domain events in the event sourcing system.
/// Domain events represent significant business occurrences that have happened in the domain
/// and are stored as part of the event stream.
/// </summary>
/// <example>
/// <code>
/// // Simple event
/// [EventType("OrderPlaced", 1)]
/// public record OrderPlacedEvent : IDomainEvent
/// {
///     public Guid OrderId { get; init; }
///     public Guid CustomerId { get; init; }
///     public DateTime PlacedAt { get; init; }
///     public decimal TotalAmount { get; init; }
///     public List&lt;OrderItem&gt; Items { get; init; } = new();
/// }
/// 
/// // Event with business context
/// [EventType("PaymentProcessed", 1)]
/// public record PaymentProcessedEvent : IDomainEvent
/// {
///     public Guid PaymentId { get; init; }
///     public Guid OrderId { get; init; }
///     public decimal Amount { get; init; }
///     public string PaymentMethod { get; init; }
///     public DateTime ProcessedAt { get; init; }
///     public string TransactionId { get; init; }
/// }
/// 
/// // Usage in aggregates
/// public class Order : AggregateRoot
/// {
///     public void PlaceOrder(CustomerId customerId, List&lt;OrderItem&gt; items)
///     {
///         var orderPlaced = new OrderPlacedEvent
///         {
///             OrderId = Id,
///             CustomerId = customerId.Id,
///             PlacedAt = DateTime.UtcNow,
///             Items = items,
///             TotalAmount = items.Sum(i =&gt; i.Price * i.Quantity)
///         };
///         
///         Add(orderPlaced); // Adds to uncommitted events
///     }
/// }
/// </code>
/// </example>
public interface IEvent;
