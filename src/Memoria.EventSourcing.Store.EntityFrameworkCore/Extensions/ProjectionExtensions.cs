using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;
using Newtonsoft.Json;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Extensions;

/// <summary>
/// Provides extension methods for converting projections (read models) to and from their
/// <see cref="ProjectionEntity"/> snapshot storage.
/// </summary>
public static class ProjectionExtensions
{
    /// <summary>
    /// Converts a projection to its corresponding <see cref="ProjectionEntity"/> snapshot for persistence.
    /// </summary>
    /// <typeparam name="T">The projection type.</typeparam>
    /// <param name="projection">The projection instance to convert.</param>
    /// <param name="streamId">The stream identifier the projection belongs to.</param>
    /// <param name="projectionId">The unique identifier of the projection.</param>
    /// <returns>A <see cref="ProjectionEntity"/> containing the serialized projection snapshot.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the projection type does not have the required <see cref="ProjectionType"/> attribute.
    /// </exception>
    public static ProjectionEntity ToProjectionEntity<T>(this IProjection projection, IStreamId streamId,
        IProjectionId<T> projectionId) where T : IProjection
    {
        var projectionTypeBindingKey = TypeBindings.GetProjectionBindingKey(projection.GetType());

        projection.StreamId = streamId.Id;
        projection.ProjectionId = projectionId.ToStoreId();

        return new ProjectionEntity
        {
            Id = projectionId.ToStoreId(),
            StreamId = streamId.Id,
            Version = projection.Version,
            LatestEventSequence = projection.LatestEventSequence,
            ProjectionType = projectionTypeBindingKey,
            Data = DomainSerializer.Current.Serialize(projection)
        };
    }

    /// <summary>
    /// Converts a <see cref="ProjectionEntity"/> snapshot back to a projection.
    /// </summary>
    /// <typeparam name="T">The projection type.</typeparam>
    /// <param name="projectionEntity">The snapshot entity.</param>
    /// <returns>The projection.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the projection type is not registered in TypeBindings.</exception>
    public static T ToProjection<T>(this ProjectionEntity projectionEntity) where T : IProjection
    {
        var typeFound = TypeBindings.ProjectionTypeBindings.TryGetValue(projectionEntity.ProjectionType, out var projectionType);
        if (typeFound is false)
        {
            throw new InvalidOperationException($"Projection type {projectionEntity.ProjectionType} not found in TypeBindings");
        }

        var projection = (T)DomainSerializer.Current.Deserialize(projectionEntity.Data, projectionType!);
        projection.StreamId = projectionEntity.StreamId;
        projection.ProjectionId = projectionEntity.Id;
        projection.Version = projectionEntity.Version;
        projection.LatestEventSequence = projectionEntity.LatestEventSequence;
        return projection;
    }
    
}
