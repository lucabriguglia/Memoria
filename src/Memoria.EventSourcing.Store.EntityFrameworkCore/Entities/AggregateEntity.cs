using Memoria.EventSourcing.Domain;
using Newtonsoft.Json;

// ReSharper disable EntityFramework.ModelValidation.UnlimitedStringLength

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;

/// <summary>
/// Represents the database entity for storing aggregate snapshots.
/// </summary>
public class AggregateEntity : IAuditableEntity, IEditableEntity
{
    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    public string Id { get; set; } = null!;

    /// <summary>
    /// Gets or sets the stream ID.
    /// </summary>
    public string StreamId { get; set; } = null!;

    /// <summary>
    /// Gets or sets the aggregate type.
    /// </summary>
    public string AggregateType { get; set; } = null!;

    /// <summary>
    /// Gets or sets the version.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Gets or sets the latest event sequence.
    /// </summary>
    public int LatestEventSequence { get; set; }

    /// <summary>
    /// Gets or sets the JSON data.
    /// </summary>
    public string Data { get; set; } = null!;

    /// <summary>
    /// Gets or sets the created date.
    /// </summary>
    public DateTimeOffset CreatedDate { get; set; }

    /// <summary>
    /// Gets or sets the created by.
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets the updated date.
    /// </summary>
    public DateTimeOffset UpdatedDate { get; set; }

    /// <summary>
    /// Gets or sets the updated by.
    /// </summary>
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Extension methods for AggregateEntity.
/// </summary>
public static class AggregateEntityExtensions
{
    /// <summary>
    /// JSON serializer settings.
    /// </summary>
    private static readonly JsonSerializerSettings JsonSerializerSettings = new()
    {
        ContractResolver = new PrivateSetterContractResolver()
    };

    /// <summary>
    /// Converts an AggregateEntity to a domain aggregate.
    /// </summary>
    /// <typeparam name="T">The aggregate type.</typeparam>
    /// <param name="aggregateEntity">The entity.</param>
    /// <returns>The aggregate.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the aggregate type is not found.</exception>
    /// <example>
    /// <code>
    /// var entity = await _context.Aggregates.FindAsync(id);
    /// var aggregate = entity.ToAggregate&lt;OrderAggregate&gt;();
    /// </code>
    /// </example>
    public static T ToAggregate<T>(this AggregateEntity aggregateEntity) where T : IAggregateRoot
    {
        var typeFound = TypeBindings.AggregateTypeBindings.TryGetValue(aggregateEntity.AggregateType, out var aggregateType);
        if (typeFound is false)
        {
            throw new InvalidOperationException($"Aggregate type {aggregateEntity.AggregateType} not found in TypeBindings");
        }

        var aggregate = (T)JsonConvert.DeserializeObject(aggregateEntity.Data, aggregateType!, JsonSerializerSettings)!;
        aggregate.StreamId = aggregateEntity.StreamId;
        aggregate.AggregateId = aggregateEntity.Id;
        aggregate.Version = aggregateEntity.Version;
        aggregate.LatestEventSequence = aggregateEntity.LatestEventSequence;
        return aggregate;
    }
}
