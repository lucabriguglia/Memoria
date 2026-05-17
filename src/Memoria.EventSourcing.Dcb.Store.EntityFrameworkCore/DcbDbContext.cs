using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Configurations;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;

/// <summary>
/// Optional base class. Inherit when you want a DbContext that owns the DCB tables
/// and nothing else; otherwise implement <see cref="IDcbDbContext"/> on your own context.
/// </summary>
public abstract class DcbDbContext(DbContextOptions options) : DbContext(options), IDcbDbContext
{
    public DbSet<DcbEventEntity> DcbEvents { get; set; } = null!;
    public DbSet<DcbEventTagEntity> DcbEventTags { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new DcbEventEntityConfiguration());
        modelBuilder.ApplyConfiguration(new DcbEventTagEntityConfiguration());
    }
}
