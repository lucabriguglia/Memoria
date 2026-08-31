using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;

/// <summary>
/// The database context surface the DCB store's extension methods work against.
/// </summary>
public interface IDcbDbContext
{
    /// <summary>
    /// Gets the appended events.
    /// </summary>
    DbSet<DcbEventEntity> DcbEvents { get; }

    /// <summary>
    /// Gets the tags on those events.
    /// </summary>
    DbSet<DcbEventTagEntity> DcbEventTags { get; }

    /// <summary>
    /// Gets the per-tag rows appends contend on.
    /// </summary>
    DbSet<DcbTagHeadEntity> DcbTagHeads { get; }

    /// <summary>
    /// Gets the persisted folds of a boundary into an aggregate or a projection.
    /// </summary>
    DbSet<DcbSnapshotEntity> DcbSnapshots { get; }

    /// <summary>
    /// Gets the change tracker.
    /// </summary>
    ChangeTracker ChangeTracker { get; }

    /// <summary>
    /// Gets the database facade, used for transactions.
    /// </summary>
    DatabaseFacade Database { get; }

    /// <summary>
    /// Saves pending changes.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The number of rows written.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
