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
/// Populated by <c>AddMemoriaDcb</c>.
/// </para>
/// </remarks>
public static class DcbTypeBindings
{
    /// <summary>
    /// Gets or sets the DCB aggregate bindings, keyed by <c>{name}:{version}</c>.
    /// </summary>
    public static Dictionary<string, Type> AggregateTypeBindings { get; set; } = new();

    /// <summary>
    /// Gets or sets the DCB projection bindings, keyed by <c>{name}:{version}</c>.
    /// </summary>
    public static Dictionary<string, Type> ProjectionTypeBindings { get; set; } = new();

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
