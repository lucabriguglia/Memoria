using System.Reflection;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;
using Newtonsoft.Json;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Extensions;

/// <summary>
/// Provides extension methods for converting projections (read models) to and from the
/// <see cref="AggregateEntity"/> snapshot storage. Projections are persisted in the same table as
/// aggregates for the time being.
/// </summary>
public static class ProjectionExtensions
{
    private static readonly JsonSerializerSettings JsonSerializerSettings = new()
    {
        ContractResolver = new PrivateSetterContractResolver()
    };

    /// <summary>
    /// Converts a projection to its corresponding <see cref="AggregateEntity"/> snapshot for persistence.
    /// </summary>
    /// <typeparam name="T">The projection type.</typeparam>
    /// <param name="projection">The projection instance to convert.</param>
    /// <param name="streamId">The stream identifier the projection belongs to.</param>
    /// <param name="projectionId">The unique identifier of the projection.</param>
    /// <returns>An <see cref="AggregateEntity"/> containing the serialized projection snapshot.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the projection type does not have the required <see cref="ProjectionType"/> attribute.
    /// </exception>
    public static AggregateEntity ToProjectionEntity<T>(this IProjection projection, IStreamId streamId,
        IProjectionId<T> projectionId) where T : IProjection
    {
        var projectionType = projection.GetType().GetCustomAttribute<ProjectionType>();
        if (projectionType == null)
        {
            throw new InvalidOperationException($"Projection {projection.GetType().Name} does not have a ProjectionType attribute.");
        }

        projection.StreamId = streamId.Id;
        projection.AggregateId = projectionId.ToStoreId();

        return new AggregateEntity
        {
            Id = projectionId.ToStoreId(),
            StreamId = streamId.Id,
            Version = projection.Version,
            LatestEventSequence = projection.LatestEventSequence,
            AggregateType = TypeBindings.GetTypeBindingKey(projectionType.Name, projectionType.Version),
            Data = JsonConvert.SerializeObject(projection)
        };
    }

    /// <summary>
    /// Converts an <see cref="AggregateEntity"/> snapshot back to a projection.
    /// </summary>
    /// <typeparam name="T">The projection type.</typeparam>
    /// <param name="aggregateEntity">The snapshot entity.</param>
    /// <returns>The projection.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the projection type is not registered in TypeBindings.</exception>
    public static T ToProjection<T>(this AggregateEntity aggregateEntity) where T : IProjection
    {
        var typeFound = TypeBindings.ProjectionTypeBindings.TryGetValue(aggregateEntity.AggregateType, out var projectionType);
        if (typeFound is false)
        {
            throw new InvalidOperationException($"Projection type {aggregateEntity.AggregateType} not found in TypeBindings");
        }

        var projection = (T)JsonConvert.DeserializeObject(aggregateEntity.Data, projectionType!, JsonSerializerSettings)!;
        projection.StreamId = aggregateEntity.StreamId;
        projection.AggregateId = aggregateEntity.Id;
        projection.Version = aggregateEntity.Version;
        projection.LatestEventSequence = aggregateEntity.LatestEventSequence;
        return projection;
    }
}
