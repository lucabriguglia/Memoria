namespace OpenCqrs.EventSourcing.Domain;

/// <summary>
/// Attribute that provides type metadata for aggregate classes, including logical name and version information.
/// This metadata is essential for aggregate serialization, deserialization, and type evolution in event stores.
/// </summary>
/// <param name="name">
/// The logical name of the aggregate type. This should be a stable identifier that remains consistent
/// even if the C# class name changes. Used for aggregate type identification during serialization/deserialization.
/// </param>
/// <param name="version">
/// The version number of the aggregate schema. Defaults to 1. Used for managing schema evolution
/// and ensuring proper deserialization of aggregates stored with different versions.
/// </param>
/// <example>
/// <code>
/// [AggregateType("Order")]
/// public class OrderAggregate : AggregateRoot
/// {
///     // Aggregate implementation
/// }
/// </code>
/// </example>
/// <example>
/// <code>
/// [AggregateType("User", 2)]
/// public class UserAggregate : AggregateRoot
/// {
///     // Aggregate implementation with version 2
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class)]
public class AggregateType(string name, byte version = 1) : Attribute
{
    /// <summary>
    /// Gets the logical name of the aggregate type.
    /// </summary>
    /// <value>
    /// A string that serves as the stable, logical identifier for this aggregate type.
    /// Used for serialization and should remain constant even if the class name changes.
    /// </value>
    public string Name { get; } = name;

    /// <summary>
    /// Gets the version number of the aggregate schema.
    /// </summary>
    /// <value>
    /// A byte value representing the schema version of this aggregate type.
    /// Used for managing schema evolution and compatibility during aggregate deserialization.
    /// </value>
    public byte Version { get; } = version;
}
