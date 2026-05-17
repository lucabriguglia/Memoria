using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;

/// <summary>
/// Contract for a DbContext that exposes the DCB event log tables.
/// User contexts implement this (or derive from <see cref="DcbDbContext"/>) so the
/// <see cref="EntityFrameworkCoreDcbStore"/> can operate against their schema.
/// </summary>
public interface IDcbDbContext : IDisposable, IAsyncDisposable
{
    DbSet<DcbEventEntity> DcbEvents { get; set; }
    DbSet<DcbEventTagEntity> DcbEventTags { get; set; }

    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
