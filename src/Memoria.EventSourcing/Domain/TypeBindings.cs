namespace Memoria.EventSourcing.Domain;

/// <summary>
/// Provides type-binding dictionaries for domain events and aggregates.
/// </summary>
public static class TypeBindings
{
    /// <summary>
    /// Gets or sets the event type bindings.
    /// </summary>
    public static Dictionary<string, Type> EventTypeBindings { get; set; } = new();

    /// <summary>
    /// Gets or sets the aggregate type bindings.
    /// </summary>
    public static Dictionary<string, Type> AggregateTypeBindings { get; set; } = new();

    /// <summary>
    /// Gets the type binding key.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="version">The version.</param>
    /// <returns>The binding key.</returns>
    public static string GetTypeBindingKey(string name, int version) => $"{name}:{version}";
}
