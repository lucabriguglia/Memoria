using System.Collections.Concurrent;
using System.Reflection;

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
    /// Gets or sets the projection type bindings.
    /// </summary>
    public static Dictionary<string, Type> ProjectionTypeBindings { get; set; } = new();

    /// <summary>
    /// Gets the type binding key.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="version">The version.</param>
    /// <returns>The binding key.</returns>
    public static string GetTypeBindingKey(string name, int version) => $"{name}:{version}";

    /// <summary>
    /// Merges newly scanned bindings into an existing binding map, additively.
    /// </summary>
    /// <remarks>
    /// The binding maps are process-wide static state written by every <c>AddMemoria*</c>
    /// registration, and more than one may run: an application using both the streamed and the
    /// dynamic-consistency-boundary model registers each, and both scan for
    /// <see cref="IEvent"/> implementations. Replacing the map would leave the earlier
    /// registration's types unresolvable, and nothing would report it — the failure would surface
    /// much later as a read that cannot deserialise.
    /// <para>
    /// Re-binding a key to the same CLR type is agreement and does nothing, so scanning one
    /// assembly twice is safe. Binding it to a <em>different</em> type is a genuine conflict:
    /// whichever won, some events would deserialise into the wrong type, so it throws instead.
    /// </para>
    /// </remarks>
    /// <param name="existing">The current bindings.</param>
    /// <param name="scanned">The bindings just discovered.</param>
    /// <param name="description">What is being bound, for the error message. For example "Event".</param>
    /// <returns>The merged bindings.</returns>
    /// <exception cref="InvalidOperationException">
    /// A binding key is claimed by two different CLR types.
    /// </exception>
    public static Dictionary<string, Type> Merge(
        Dictionary<string, Type> existing, Dictionary<string, Type> scanned, string description)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(scanned);

        var merged = new Dictionary<string, Type>(existing);

        foreach (var (key, clrType) in scanned)
        {
            if (merged.TryGetValue(key, out var boundType) && boundType != clrType)
            {
                throw new InvalidOperationException(
                    $"{description} binding key '{key}' is claimed by two different types: '{boundType.FullName}' and '{clrType.FullName}'. " +
                    "Binding keys come from the type's attribute name and version, so give one of them a different name or version.");
            }

            merged[key] = clrType;
        }

        return merged;
    }

    /// <summary>
    /// Gets the binding key for an event CLR type, reading its <see cref="EventType"/> attribute once
    /// per type.
    /// </summary>
    /// <param name="eventClrType">The event CLR type.</param>
    /// <returns>The binding key.</returns>
    /// <exception cref="InvalidOperationException">The type has no <see cref="EventType"/> attribute.</exception>
    public static string GetEventBindingKey(Type eventClrType) =>
        EventBindingKeys.GetOrAdd(eventClrType, static clrType =>
        {
            var eventType = clrType.GetCustomAttribute<EventType>();
            if (eventType == null)
            {
                throw new InvalidOperationException($"Event {clrType.Name} does not have a EventType attribute.");
            }

            return GetTypeBindingKey(eventType.Name, eventType.Version);
        });

    /// <summary>
    /// Gets the binding key for an aggregate CLR type, reading its <see cref="AggregateType"/>
    /// attribute once per type.
    /// </summary>
    /// <param name="aggregateClrType">The aggregate CLR type.</param>
    /// <returns>The binding key.</returns>
    /// <exception cref="InvalidOperationException">The type has no <see cref="AggregateType"/> attribute.</exception>
    public static string GetAggregateBindingKey(Type aggregateClrType) =>
        AggregateBindingKeys.GetOrAdd(aggregateClrType, static clrType =>
        {
            var aggregateType = clrType.GetCustomAttribute<AggregateType>();
            if (aggregateType == null)
            {
                throw new InvalidOperationException($"Aggregate {clrType.Name} does not have a AggregateType attribute.");
            }

            return GetTypeBindingKey(aggregateType.Name, aggregateType.Version);
        });

    /// <summary>
    /// Gets the binding key for a projection CLR type, reading its <see cref="ProjectionType"/>
    /// attribute once per type.
    /// </summary>
    /// <param name="projectionClrType">The projection CLR type.</param>
    /// <returns>The binding key.</returns>
    /// <exception cref="InvalidOperationException">The type has no <see cref="ProjectionType"/> attribute.</exception>
    public static string GetProjectionBindingKey(Type projectionClrType) =>
        ProjectionBindingKeys.GetOrAdd(projectionClrType, static clrType =>
        {
            var projectionType = clrType.GetCustomAttribute<ProjectionType>();
            if (projectionType == null)
            {
                throw new InvalidOperationException($"Projection {clrType.Name} does not have a ProjectionType attribute.");
            }

            return GetTypeBindingKey(projectionType.Name, projectionType.Version);
        });

    /// <summary>
    /// Gets <see cref="EventTypeBindings"/> inverted, for looking up a binding key by CLR type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Event type filters arrive as CLR types but are stored as binding keys, so every filtered read
    /// needs this direction. Scanning <see cref="EventTypeBindings"/> for each requested type is
    /// O(bindings) per type, per query.
    /// </para>
    /// <para>
    /// The cache is keyed on the dictionary <em>instance</em>, so assigning a new
    /// <see cref="EventTypeBindings"/> rebuilds it. Publication is a single reference assignment, so
    /// a concurrent rebuild wastes work but cannot be observed half-built.
    /// </para>
    /// <para>
    /// When several keys bind the same CLR type the first one wins, matching the scan this replaced.
    /// A type with no binding is simply absent; callers use <c>GetValueOrDefault</c> and get null,
    /// which is also what the scan produced.
    /// </para>
    /// </remarks>
    /// <returns>Binding keys by CLR type.</returns>
    public static Dictionary<Type, string> GetEventBindingKeysByType()
    {
        var source = EventTypeBindings;

        var cached = _cachedEventBindingKeysByType;
        if (cached is not null && ReferenceEquals(cached.Source, source))
        {
            return cached.BindingKeysByType;
        }

        var bindingKeysByType = new Dictionary<Type, string>();
        foreach (var binding in source)
        {
            bindingKeysByType.TryAdd(binding.Value, binding.Key);
        }

        _cachedEventBindingKeysByType = new ReverseEventTypeBindings(source, bindingKeysByType);
        return bindingKeysByType;
    }

    private sealed record ReverseEventTypeBindings(
        Dictionary<string, Type> Source,
        Dictionary<Type, string> BindingKeysByType);

    private static ReverseEventTypeBindings? _cachedEventBindingKeysByType;

    private static readonly ConcurrentDictionary<Type, string> EventBindingKeys = new();
    private static readonly ConcurrentDictionary<Type, string> AggregateBindingKeys = new();
    private static readonly ConcurrentDictionary<Type, string> ProjectionBindingKeys = new();
}