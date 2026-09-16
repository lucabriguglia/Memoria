namespace Memoria.EventSourcing.Domain;

/// <summary>
/// One complete set of type bindings: the maps a store resolves a stored key through to find the CLR
/// type that wrote it, and the inverted event map a filtered read turns CLR types back into keys with.
/// </summary>
/// <remarks>
/// <para>
/// There is one process-wide instance, <see cref="Default"/>, and it is what the static properties on
/// <see cref="TypeBindings"/> and <c>DcbTypeBindings</c> read and write — so an application that
/// registers its types through <c>AddMemoriaEventSourcing</c> or <c>AddMemoriaDcb</c> and never
/// mentions this type is bound exactly as before.
/// </para>
/// <para>
/// A store given an instance of its own resolves through that instance instead. That is what lets a
/// host read two bounded contexts' stores in one process when both declare an event, aggregate or
/// projection under the same name and version: each store carries the set its own assemblies were
/// scanned into, and the two never meet.
/// </para>
/// <para>
/// Each map is a whole dictionary that is assigned, not mutated: a reader sees either the previous
/// dictionary or the new one, never half of each. The inverted event map is rebuilt when
/// <see cref="EventTypeBindings"/> is assigned a different dictionary and cached until then.
/// </para>
/// </remarks>
public sealed class TypeBindingSet
{
    /// <summary>
    /// The process-wide set, which the static properties on <see cref="TypeBindings"/> are views
    /// over, and which every store reads unless given a set of its own.
    /// </summary>
    public static TypeBindingSet Default { get; } = new();

    /// <summary>Gets or sets the event bindings, keyed by <c>{name}:{version}</c>.</summary>
    public Dictionary<string, Type> EventTypeBindings { get; set; } = new();

    /// <summary>Gets or sets the streamed aggregate bindings, keyed by <c>{name}:{version}</c>.</summary>
    public Dictionary<string, Type> AggregateTypeBindings { get; set; } = new();

    /// <summary>Gets or sets the streamed projection bindings, keyed by <c>{name}:{version}</c>.</summary>
    public Dictionary<string, Type> ProjectionTypeBindings { get; set; } = new();

    /// <summary>
    /// Gets or sets the DCB aggregate bindings, keyed by <c>{name}:{version}</c>. Kept apart from
    /// <see cref="AggregateTypeBindings"/> because the two consistency models may name the same
    /// concept — see <c>DcbTypeBindings</c>.
    /// </summary>
    public Dictionary<string, Type> DcbAggregateTypeBindings { get; set; } = new();

    /// <summary>Gets or sets the DCB projection bindings, keyed by <c>{name}:{version}</c>.</summary>
    public Dictionary<string, Type> DcbProjectionTypeBindings { get; set; } = new();

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
    /// When several keys bind the same CLR type the first one wins. A type with no binding is simply
    /// absent; callers use <c>GetValueOrDefault</c> and get null.
    /// </para>
    /// </remarks>
    /// <returns>Binding keys by CLR type.</returns>
    public Dictionary<Type, string> GetEventBindingKeysByType()
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

    private ReverseEventTypeBindings? _cachedEventBindingKeysByType;
}
