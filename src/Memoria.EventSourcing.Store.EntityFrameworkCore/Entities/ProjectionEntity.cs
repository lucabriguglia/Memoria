// ReSharper disable EntityFramework.ModelValidation.UnlimitedStringLength

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;

/// <summary>
/// Represents the database entity for storing projection (read model) snapshots.
/// </summary>
public class ProjectionEntity : IAuditableEntity, IEditableEntity
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
    /// Gets or sets the projection type, typically represented in a "Name:Version" format.
    /// </summary>
    public string ProjectionType { get; set; } = null!;

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
