using System.Collections.Concurrent;
using System.Reflection;

namespace Memoria.EventSourcing.Domain;

/// <summary>
/// Defines a contract for projection identifiers.
/// </summary>
/// <example>
/// <code>
/// public class OrderSummaryId : IProjectionId&lt;OrderSummary&gt;
/// {
///     public string Id { get; }
///     public IDictionary&lt;string, string&gt;? EventPropertyFilter =&gt; null;
///     public OrderSummaryId(Guid id) =&gt; Id = id.ToString();
/// }
/// </code>
/// </example>
/// <example>
/// <code>
/// // One projection per order, built from a stream the whole customer shares.
/// public class OrderSummaryId : IProjectionId&lt;OrderSummary&gt;
/// {
///     public string Id { get; }
///
///     public IDictionary&lt;string, string&gt;? EventPropertyFilter { get; }
///
///     public OrderSummaryId(Guid orderId)
///     {
///         Id = orderId.ToString();
///         EventPropertyFilter = new Dictionary&lt;string, string&gt; { ["OrderId"] = orderId.ToString() };
///     }
/// }
/// </code>
/// </example>
public interface IProjectionId
{
    /// <summary>
    /// Gets the unique string identifier.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Specifies a filter applied to properties of events.
    /// </summary>
    /// <remarks>
    /// The same mechanism as <see cref="IAggregateId.EventPropertyFilter"/>, and needed for the same
    /// reason: when several models share one stream, the stream alone does not say which events
    /// belong to this one. A read model is no less likely to share a stream than a write model, so it
    /// narrows the same way. Return null when the stream holds only this projection's events.
    /// </remarks>
    IDictionary<string, string>? EventPropertyFilter { get; }
}

/// <summary>
/// Defines a strongly-typed contract for projection identifiers.
/// </summary>
/// <typeparam name="T">The projection type.</typeparam>
public interface IProjectionId<T> : IProjectionId where T : IProjection;

/// <summary>
/// Extension methods for IProjectionId.
/// </summary>
public static class IProjectionIdExtensions
{
    /// <summary>
    /// Combines the projection ID with its type version to form the persisted store identifier.
    /// </summary>
    /// <param name="projectionId">The projection identifier.</param>
    /// <returns>The store ID.</returns>
    public static string ToStoreId<T>(this IProjectionId<T> projectionId) where T : IProjection =>
        $"{projectionId.Id}:{GetVersion(typeof(T))}";

    // Resolved once per closed generic rather than per call. A throwing factory caches nothing, so
    // an unattributed type still throws on every call rather than turning into a
    // TypeInitializationException.
    private static int GetVersion(Type projectionClrType) => Versions.GetOrAdd(projectionClrType, static clrType =>
    {
        var projectionType = clrType.GetCustomAttribute<ProjectionType>();
        if (projectionType == null)
        {
            throw new InvalidOperationException($"Projection {clrType.Name} does not have a ProjectionType attribute.");
        }

        return projectionType.Version;
    });

    private static readonly ConcurrentDictionary<Type, int> Versions = new();
}
