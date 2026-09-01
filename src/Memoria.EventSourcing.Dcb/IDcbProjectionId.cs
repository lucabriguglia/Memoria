using System.Collections.Concurrent;
using System.Reflection;
using Memoria.EventSourcing.Domain;

namespace Memoria.EventSourcing.Dcb;

/// <summary>
/// Defines a contract for DCB projection identifiers.
/// </summary>
/// <example>
/// <code>
/// public class SeatSummaryId(string seatId) : IDcbProjectionId&lt;SeatSummaryProjection&gt;
/// {
///     public string Id { get; } = seatId;
///     public TagQuery Boundary { get; } = TagQuery.AnyOf(new Tag("seat", seatId));
/// }
/// </code>
/// </example>
public interface IDcbProjectionId
{
    /// <summary>
    /// Gets the unique string identifier.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the consistency boundary this projection is folded from.
    /// </summary>
    /// <remarks>
    /// Must be stable for a given <see cref="Id"/>, for the reason given on
    /// <see cref="IDcbAggregateId.Boundary"/>.
    /// </remarks>
    TagQuery Boundary { get; }
}

/// <summary>
/// Defines a strongly-typed contract for DCB projection identifiers.
/// </summary>
/// <typeparam name="T">The projection type.</typeparam>
public interface IDcbProjectionId<T> : IDcbProjectionId where T : IDcbProjection;

/// <summary>
/// Extension methods for <see cref="IDcbProjectionId{T}"/>.
/// </summary>
public static class DcbProjectionIdExtensions
{
    /// <summary>
    /// Combines the projection ID with its type version to form the persisted snapshot identifier.
    /// </summary>
    /// <typeparam name="T">The projection type.</typeparam>
    /// <param name="projectionId">The projection identifier.</param>
    /// <returns>The store ID.</returns>
    public static string ToStoreId<T>(this IDcbProjectionId<T> projectionId) where T : IDcbProjection =>
        $"{projectionId.Id}:{GetVersion(typeof(T))}";

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
