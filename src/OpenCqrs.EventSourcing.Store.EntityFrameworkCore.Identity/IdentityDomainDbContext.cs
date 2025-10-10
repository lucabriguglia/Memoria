using Memoria.EventSourcing.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Configurations;
using OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Entities;
using OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Interceptors;

namespace OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Identity;

public abstract class IdentityDomainDbContext(
    DbContextOptions<IdentityDomainDbContext> options,
    TimeProvider timeProvider,
    IHttpContextAccessor httpContextAccessor)
    : IdentityDbContext<IdentityUser>(options), IDomainDbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        optionsBuilder.AddInterceptors(new AuditInterceptor(timeProvider, httpContextAccessor));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new AggregateEntityConfiguration());
        modelBuilder.ApplyConfiguration(new EventEntityConfiguration());
        modelBuilder.ApplyConfiguration(new AggregateEventEntityConfiguration());
    }

    public DbSet<AggregateEntity> Aggregates { get; set; } = null!;
    public DbSet<EventEntity> Events { get; set; } = null!;
    public DbSet<AggregateEventEntity> AggregateEvents { get; set; } = null!;

    public void DetachAggregate<T>(IAggregateId<T> aggregateId, T aggregate) where T : IAggregateRoot
    {
        foreach (var entityEntry in ChangeTracker.Entries())
        {
            if (entityEntry.Entity is not AggregateEntity aggregateEntity)
            {
                continue;
            }

            if (aggregateEntity.Id == aggregateId.ToStoreId())
            {
                entityEntry.State = EntityState.Detached;
            }
        }
    }
}
