namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;

/// <summary>
/// An entity whose last modification is stamped automatically by
/// <see cref="Interceptors.AuditInterceptor"/>.
/// </summary>
/// <remarks>
/// Only snapshots implement this. Events are append-only and are never edited, which is why
/// <see cref="DcbEventEntity"/> is auditable but not editable.
/// </remarks>
public interface IEditableEntity
{
    /// <summary>
    /// Gets or sets the date the entity was last updated.
    /// </summary>
    DateTimeOffset UpdatedDate { get; set; }

    /// <summary>
    /// Gets or sets the user that last updated the entity.
    /// </summary>
    string? UpdatedBy { get; set; }
}
