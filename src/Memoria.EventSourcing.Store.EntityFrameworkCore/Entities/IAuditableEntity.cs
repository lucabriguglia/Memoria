namespace OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Entities;

/// <summary>
/// Defines the contract for entities with creation audit trail.
/// </summary>
public interface IAuditableEntity
{
    /// <summary>
    /// Gets or sets the created date.
    /// </summary>
    DateTimeOffset CreatedDate { get; set; }

    /// <summary>
    /// Gets or sets the created by.
    /// </summary>
    string? CreatedBy { get; set; }
}
