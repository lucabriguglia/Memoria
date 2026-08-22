namespace Memoria.EventSourcing.Domain;

/// <summary>
/// Attribute that provides type metadata for projection (read-model) classes, including logical name
/// and version information. This metadata is used for projection snapshot serialization,
/// deserialization, and type evolution in the store.
/// </summary>
/// <param name="name">
/// The logical name of the projection type. This should be a stable identifier that remains consistent
/// even if the C# class name changes. Used for projection type identification during serialization/deserialization.
/// </param>
/// <param name="version">
/// The version number of the projection schema. Defaults to 1. Used for managing schema evolution
/// and ensuring proper deserialization of projections stored with different versions.
/// </param>
/// <example>
/// <code>
/// [ProjectionType("OrderSummary")]
/// public class OrderSummary : Projection
/// {
///     // Projection implementation
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class)]
public class ProjectionType(string name, byte version = 1) : Attribute
{
    /// <summary>
    /// Gets the logical name of the projection type.
    /// </summary>
    /// <value>
    /// A string that serves as the stable, logical identifier for this projection type.
    /// Used for serialization and should remain constant even if the class name changes.
    /// </value>
    public string Name { get; } = name;

    /// <summary>
    /// Gets the version number of the projection schema.
    /// </summary>
    /// <value>
    /// A byte value representing the schema version of this projection type.
    /// Used for managing schema evolution and compatibility during projection deserialization.
    /// </value>
    public byte Version { get; } = version;
}
