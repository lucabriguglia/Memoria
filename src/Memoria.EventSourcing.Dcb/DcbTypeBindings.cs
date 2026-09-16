using Memoria.EventSourcing.Domain;

namespace Memoria.EventSourcing.Dcb;

/// <summary>
/// Maps binding keys to the CLR types of DCB aggregates and projections, so a stored snapshot can
/// be deserialised back into the type that wrote it.
/// </summary>
/// <remarks>
/// Aggregates and projections are bound here rather than in <see cref="TypeBindings"/> because the
/// two consistency models may legitimately name the same concept: an application migrating to DCB
/// can have both a streamed <c>Seat</c> and a DCB <c>Seat</c>, and neither should have to be
/// renamed. Events are the opposite case and stay in <see cref="TypeBindings.EventTypeBindings"/> —
/// an event is the same event whichever model appends it, and two CLR types claiming one key is a
/// real bug.
/// <para>
/// Populated by <c>AddMemoriaDcb</c>. Like <see cref="TypeBindings"/>, the two maps here are views
/// over <see cref="TypeBindingSet.Default"/>; a store given a set of its own never consults them.
/// </para>
/// </remarks>
public static class DcbTypeBindings
{
    /// <summary>
    /// Gets or sets the DCB aggregate bindings of the process-wide set, keyed by <c>{name}:{version}</c>.
    /// </summary>
    public static Dictionary<string, Type> AggregateTypeBindings
    {
        get => TypeBindingSet.Default.DcbAggregateTypeBindings;
        set => TypeBindingSet.Default.DcbAggregateTypeBindings = value;
    }

    /// <summary>
    /// Gets or sets the DCB projection bindings of the process-wide set, keyed by <c>{name}:{version}</c>.
    /// </summary>
    public static Dictionary<string, Type> ProjectionTypeBindings
    {
        get => TypeBindingSet.Default.DcbProjectionTypeBindings;
        set => TypeBindingSet.Default.DcbProjectionTypeBindings = value;
    }

    /// <summary>
    /// Resolves a DCB aggregate's binding key from its <see cref="AggregateType"/> attribute.
    /// </summary>
    /// <param name="aggregateClrType">The aggregate type.</param>
    /// <returns>The binding key.</returns>
    /// <exception cref="InvalidOperationException">The type has no attribute.</exception>
    public static string GetAggregateBindingKey(Type aggregateClrType) =>
        TypeBindings.GetAggregateBindingKey(aggregateClrType);

    /// <summary>
    /// Resolves a DCB projection's binding key from its <see cref="ProjectionType"/> attribute.
    /// </summary>
    /// <param name="projectionClrType">The projection type.</param>
    /// <returns>The binding key.</returns>
    /// <exception cref="InvalidOperationException">The type has no attribute.</exception>
    public static string GetProjectionBindingKey(Type projectionClrType) =>
        TypeBindings.GetProjectionBindingKey(projectionClrType);
}
