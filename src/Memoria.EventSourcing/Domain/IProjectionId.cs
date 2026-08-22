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
///     public OrderSummaryId(Guid id) =&gt; Id = id.ToString();
/// }
/// </code>
/// </example>
public interface IProjectionId
{
    /// <summary>
    /// Gets the unique string identifier.
    /// </summary>
    string Id { get; }
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
    public static string ToStoreId<T>(this IProjectionId<T> projectionId) where T : IProjection
    {
        var projectionType = typeof(T).GetCustomAttribute<ProjectionType>();
        if (projectionType == null)
        {
            throw new InvalidOperationException($"Projection {typeof(T).Name} does not have a ProjectionType attribute.");
        }

        return $"{projectionId.Id}:{projectionType.Version}";
    }
}
