using System.Collections.Concurrent;
using System.Reflection;

namespace Memoria.EventSourcing.Domain;

/// <summary>
/// Defines a contract for aggregate identifiers.
/// </summary>
/// <example>
/// <code>
/// public class OrderId : IAggregateId
/// {
///     public string Id { get; }
///     public OrderId(Guid id) =&gt; Id = id.ToString();
/// }
/// </code>
/// </example>
/// <example>
/// <code>
/// public class CustomerId : IAggregateId
/// {
///     public string Id { get; }
///     public CustomerId(string id) =&gt; Id = id;
/// }
/// </code>
/// </example>
public interface IAggregateId
{
    /// <summary>
    /// Gets the unique string identifier.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Specifies a filter applied to properties of events.
    /// </summary>
    IDictionary<string, string>? EventPropertyFilter { get; }
}

/// <summary>
/// Defines a strongly-typed contract for aggregate identifiers.
/// </summary>
/// <typeparam name="T">The aggregate type.</typeparam>
public interface IAggregateId<T> : IAggregateId where T : IAggregateRoot;

/// <summary>
/// Extension methods for IAggregateId.
/// </summary>
public static class IAggregateIdExtensions
{
    /// <summary>
    /// Combines the aggregate ID with type version.
    /// </summary>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <returns>The store ID.</returns>
    public static string ToStoreId<T>(this IAggregateId<T> aggregateId) where T : IAggregateRoot =>
        $"{aggregateId.Id}:{GetVersion(typeof(T))}";

    // Called twice per event document written, so the attribute lookup is resolved once per closed
    // generic instead. A throwing factory caches nothing, so an unattributed type still throws on
    // every call rather than turning into a TypeInitializationException.
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