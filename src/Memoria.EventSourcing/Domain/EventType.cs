namespace Memoria.EventSourcing.Domain;

/// <summary>
/// Attribute that provides type metadata for domain events, including logical name and version information.
/// This metadata is essential for event serialization, deserialization, and schema evolution in event stores.
/// </summary>
/// <param name="name">
/// The logical name of the event type. This should be a stable identifier that remains consistent
/// even if the C# class name changes. Used for event type identification during serialization/deserialization.
/// </param>
/// <param name="version">
/// The version number of the event schema. Defaults to 1. Used for managing schema evolution
/// and ensuring proper deserialization of events stored with different versions.
/// </param>
/// <example>
/// <code>
/// // Basic event with default version
/// [EventType("UserRegistered")]
/// public record UserRegisteredEvent : IDomainEvent
/// {
///     public string UserId { get; init; }
///     public string Email { get; init; }
///     public DateTime RegisteredAt { get; init; }
/// }
/// 
/// // Versioned event for schema evolution
/// [EventType("OrderPlaced", 2)]
/// public record OrderPlacedEventV2 : IDomainEvent
/// {
///     public Guid OrderId { get; init; }
///     public Guid CustomerId { get; init; }
///     public DateTime PlacedAt { get; init; }
///     public decimal TotalAmount { get; init; }
///     public string Currency { get; init; } = "USD"; // New in v2
///     public List&lt;OrderItem&gt; Items { get; init; } = new();
/// }
/// 
/// // Event name different from class name
/// [EventType("customer-address-changed", 1)]
/// public record CustomerAddressUpdatedEvent : IDomainEvent
/// {
///     public Guid CustomerId { get; init; }
///     public Address OldAddress { get; init; }
///     public Address NewAddress { get; init; }
///     public DateTime ChangedAt { get; init; }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class)]
public class EventType(string name, byte version = 1) : Attribute
{
    /// <summary>
    /// Gets the logical name of the event type.
    /// </summary>
    /// <value>
    /// A string that serves as the stable, logical identifier for this event type.
    /// This name is used for serialization and should remain constant even if the class name changes.
    /// </value>
    public string Name { get; } = name;

    /// <summary>
    /// Gets the version number of the event schema.
    /// </summary>
    /// <value>
    /// A byte value representing the schema version of this event type.
    /// Used for managing schema evolution and compatibility during event deserialization.
    /// </value>
    public byte Version { get; } = version;
}