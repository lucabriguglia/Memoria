using System.Reflection;

namespace OpenCqrs.EventSourcing.Domain;

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
    public static string ToStoreId<T>(this IAggregateId<T> aggregateId) where T : IAggregateRoot
    {
        var aggregateType = typeof(T).GetCustomAttribute<AggregateType>();
        if (aggregateType == null)
        {
            throw new InvalidOperationException($"Aggregate {typeof(T).Name} does not have a AggregateType attribute.");
        }

        return $"{aggregateId.Id}:{aggregateType.Version}";
    }
}
