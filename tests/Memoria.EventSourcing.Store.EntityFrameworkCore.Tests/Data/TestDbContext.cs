using Memoria.EventSourcing.Store.EntityFrameworkCore.Tests.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Tests.Data;

public sealed class TestDbContext(
    DbContextOptions<DomainDbContext> options,
    TimeProvider timeProvider,
    IHttpContextAccessor httpContextAccessor)
    : DomainDbContext(options, timeProvider, httpContextAccessor)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder
            .Entity<TestItemEntity>()
            .ToTable(name: "Items");
    }

    public DbSet<TestItemEntity> Items { get; set; } = null!;
}
