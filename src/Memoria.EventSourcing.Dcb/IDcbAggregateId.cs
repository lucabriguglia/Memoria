using System.Collections.Concurrent;
using System.Reflection;
using Memoria.EventSourcing.Domain;

namespace Memoria.EventSourcing.Dcb;

/// <summary>
/// Defines a contract for DCB aggregate identifiers.
/// </summary>
/// <remarks>
/// Unlike <see cref="IAggregateId"/> this carries no event property filter. That existed to pick one
/// aggregate's events out of a shared stream; tags do that job directly, so a DCB identifier only
/// ever names the snapshot.
/// </remarks>
/// <example>
/// <code>
/// public class SeatId(string id) : IDcbAggregateId&lt;SeatAggregate&gt;
/// {
///     public string Id { get; } = id;
/// }
/// </code>
/// </example>
public interface IDcbAggregateId
{
    /// <summary>
    /// Gets the unique string identifier.
    /// </summary>
    string Id { get; }
}

/// <summary>
/// Defines a strongly-typed contract for DCB aggregate identifiers.
/// </summary>
/// <typeparam name="T">The aggregate type.</typeparam>
public interface IDcbAggregateId<T> : IDcbAggregateId where T : IDcbAggregateRoot;

/// <summary>
/// Extension methods for <see cref="IDcbAggregateId{T}"/>.
/// </summary>
public static class DcbAggregateIdExtensions
{
    /// <summary>
    /// Combines the aggregate ID with its type version to form the persisted snapshot identifier.
    /// </summary>
    /// <typeparam name="T">The aggregate type.</typeparam>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <returns>The store ID.</returns>
    public static string ToStoreId<T>(this IDcbAggregateId<T> aggregateId) where T : IDcbAggregateRoot =>
        $"{aggregateId.Id}:{GetVersion(typeof(T))}";

    // Resolved once per closed generic rather than by reflection on every call. A throwing factory
    // caches nothing, so an unattributed type still throws on every call rather than turning into a
    // TypeInitializationException.
    private static int GetVersion(Type aggregateClrType) => Versions.GetOrAdd(aggregateClrType, static clrType =>
    {
        var aggregateType = clrType.GetCustomAttribute<AggregateType>();
        if (aggregateType == null)
        {
            throw new InvalidOperationException($"Aggregate {clrType.Name} does not have a AggregateType attribute.");
        }

        return aggregateType.Version;
    });

    private static readonly ConcurrentDictionary<Type, int> Versions = new();
}
