using System.Reflection;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.Cosmos.Documents;
using Newtonsoft.Json;

namespace Memoria.EventSourcing.Store.Cosmos.Extensions;

/// <summary>
/// Provides extension methods for converting projections (read models) to and from their
/// <see cref="ProjectionDocument"/> snapshot storage. Projection documents share the aggregate container
/// and are distinguished by their <c>documentType</c>.
/// </summary>
public static class ProjectionExtensions
{
    /// <summary>
    /// Converts a projection to a <see cref="ProjectionDocument"/> snapshot for storage in Cosmos DB.
    /// </summary>
    /// <typeparam name="T">The projection type.</typeparam>
    /// <param name="projection">The projection instance to convert.</param>
    /// <param name="streamId">The stream identifier the projection belongs to.</param>
    /// <param name="projectionId">The unique identifier of the projection.</param>
    /// <returns>A <see cref="ProjectionDocument"/> containing the serialized projection snapshot.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the projection type does not have the required <see cref="ProjectionType"/> attribute.
    /// </exception>
    public static ProjectionDocument ToProjectionDocument<T>(this IProjection projection, IStreamId streamId,
        IProjectionId<T> projectionId) where T : IProjection
    {
        projection.StreamId = streamId.Id;
        projection.ProjectionId = projectionId.ToStoreId();

        return new ProjectionDocument
        {
            Id = projectionId.ToStoreId(),
            StreamId = streamId.Id,
            Version = projection.Version,
            LatestEventSequence = projection.LatestEventSequence,
            ProjectionType = TypeBindings.GetProjectionBindingKey(projection.GetType()),
            Data = DomainSerializer.Current.Serialize(projection)
        };
    }

    /// <summary>
    /// Converts a <see cref="ProjectionDocument"/> snapshot back to a projection.
    /// </summary>
    /// <typeparam name="T">The projection type.</typeparam>
    /// <param name="projectionDocument">The snapshot document.</param>
    /// <returns>The projection.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the projection type is not registered in TypeBindings.</exception>
    public static T ToProjection<T>(this ProjectionDocument projectionDocument) where T : IProjection
    {
        var typeFound = TypeBindings.ProjectionTypeBindings.TryGetValue(projectionDocument.ProjectionType, out var projectionType);
        if (typeFound is false)
        {
            throw new InvalidOperationException($"Projection type {projectionDocument.ProjectionType} not found in TypeBindings");
        }

        var projection = (T)DomainSerializer.Current.Deserialize(projectionDocument.Data, projectionType!);
        projection.StreamId = projectionDocument.StreamId;
        projection.ProjectionId = projectionDocument.Id;
        projection.Version = projectionDocument.Version;
        projection.LatestEventSequence = projectionDocument.LatestEventSequence;
        return projection;
    }
}
