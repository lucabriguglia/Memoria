using System.Collections.Concurrent;
using System.Reflection;
using Memoria.EventSourcing.Domain;

namespace Memoria.EventSourcing.Dcb;

/// <summary>
/// Defines a contract for DCB aggregate identifiers.
/// </summary>
/// <remarks>
/// <para>
/// Where <see cref="IAggregateId"/> carries an event property filter to pick one aggregate's events
/// out of a shared stream, this carries the boundary itself. Tags do both jobs at once, so a DCB
/// identifier names the model <em>and</em> what that model is folded from.
/// </para>
/// <para>
/// Binding the two makes them impossible to disagree. It does not fix a boundary at design time the
/// way a stream does: the identifier is constructed per decision, so its boundary varies with it.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // One entity: the boundary is that entity's tag.
/// public class SeatId(string seatId) : IDcbAggregateId&lt;SeatAggregate&gt;
/// {
///     public string Id { get; } = seatId;
///     public TagQuery Boundary { get; } = TagQuery.AnyOf(new Tag("seat", seatId));
/// }
///
/// // A decision spanning two: the boundary names both, and the identity names the pair.
/// public class SubscriptionDecisionId(string courseId, string studentId)
///     : IDcbAggregateId&lt;SubscriptionDecision&gt;
/// {
///     public string Id { get; } = $"{courseId}-{studentId}";
///     public TagQuery Boundary { get; } =
///         TagQuery.AnyOf(new Tag("course", courseId), new Tag("student", studentId));
/// }
/// </code>
/// </example>
public interface IDcbAggregateId
{
    /// <summary>
    /// Gets the unique string identifier.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the consistency boundary this aggregate is folded from.
    /// </summary>
    /// <remarks>
    /// Must be stable for a given <see cref="Id"/>. A snapshot records the boundary that produced it
    /// and is only returned for that boundary, so an identifier whose boundary varies between
    /// instances would miss its own snapshots and rebuild them — wasteful, but never wrong.
    /// </remarks>
    TagQuery Boundary { get; }
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
