namespace OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Entities;

/// <summary>
/// Defines the contract for entities with modification audit trail.
/// </summary>
public interface IEditableEntity
{
    /// <summary>
    /// Gets or sets the updated date.
    /// </summary>
    DateTimeOffset UpdatedDate { get; set; }

    /// <summary>
    /// Gets or sets the updated by.
    /// </summary>
    string? UpdatedBy { get; set; }
}
