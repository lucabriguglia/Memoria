using System.Reflection;
using Memoria.EventSourcing.Domain;
using Newtonsoft.Json;
using OpenCqrs.EventSourcing.Store.Cosmos.Documents;

namespace OpenCqrs.EventSourcing.Store.Cosmos.Extensions;

/// <summary>
/// Provides extension methods for converting aggregate objects to documents suitable for storage in Cosmos DB.
/// These methods facilitate the transformation between domain aggregates and their document representations.
/// </summary>
public static class AggregateExtensions
{
    /// <summary>
    /// Converts an aggregate to an aggregate document for storage in Cosmos DB.
    /// This method extracts aggregate metadata, updates the aggregate properties, and serializes it into a document format.
    /// </summary>
    /// <param name="aggregate">The aggregate to convert to a document.</param>
    /// <param name="streamId">The stream identifier associated with the aggregate.</param>
    /// <param name="aggregateId">The unique identifier of the aggregate.</param>
    /// <param name="newLatestEventSequence">The latest event sequence number for the aggregate.</param>
    /// <returns>An <see cref="AggregateDocument"/> containing the serialized aggregate data and metadata.</returns>
    /// <exception cref="Exception">Thrown when the aggregate type does not have an AggregateType attribute.</exception>
    public static AggregateDocument ToAggregateDocument<T>(this IAggregateRoot aggregate, IStreamId streamId, IAggregateId<T> aggregateId, int newLatestEventSequence) where T : IAggregateRoot
    {
        var aggregateType = aggregate.GetType().GetCustomAttribute<AggregateType>();
        if (aggregateType == null)
        {
            throw new InvalidOperationException($"Aggregate {aggregate.GetType().Name} does not have a AggregateType attribute.");
        }

        aggregate.StreamId = streamId.Id;
        aggregate.AggregateId = aggregateId.ToStoreId();
        aggregate.LatestEventSequence = newLatestEventSequence;

        return new AggregateDocument
        {
            Id = aggregateId.ToStoreId(),
            StreamId = streamId.Id,
            Version = aggregate.Version,
            LatestEventSequence = newLatestEventSequence,
            AggregateType = TypeBindings.GetTypeBindingKey(aggregateType.Name, aggregateType.Version),
            Data = JsonConvert.SerializeObject(aggregate)
        };
    }
}
