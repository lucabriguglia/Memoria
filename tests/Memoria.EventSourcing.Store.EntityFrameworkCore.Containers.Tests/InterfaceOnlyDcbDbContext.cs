using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Configurations;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests;

/// <summary>
/// A DCB context that implements <see cref="IDcbDbContext"/> without deriving from
/// <see cref="DcbDbContext"/>, as one that has to derive from something else — an Identity context,
/// say — would. The store appends through Entity Framework Core for such a context on every engine,
/// so this is how the container suite reaches that path once the engines' appends go through their
/// routines.
/// </summary>
/// <remarks>
/// Maps the store's tables exactly as <see cref="DcbDbContext"/> does, case-sensitive tag collation
/// included, and stamps audit columns with the same interceptor.
/// </remarks>
public sealed class InterfaceOnlyDcbDbContext(
    DbContextOptions<InterfaceOnlyDcbDbContext> options,
    TimeProvider timeProvider,
    IHttpContextAccessor httpContextAccessor)
    : DbContext(options), IDcbDbContext
{
    public DbSet<DcbEventEntity> DcbEvents { get; set; } = null!;

    public DbSet<DcbEventTagEntity> DcbEventTags { get; set; } = null!;

    public DbSet<DcbTagHeadEntity> DcbTagHeads { get; set; } = null!;

    public DbSet<DcbSnapshotEntity> DcbSnapshots { get; set; } = null!;

    public static InterfaceOnlyDcbDbContext OnSqlServer(string connectionString, params IInterceptor[] interceptors) =>
        Build(builder => builder.UseSqlServer(connectionString), interceptors);

    public static InterfaceOnlyDcbDbContext OnPostgreSql(string connectionString, params IInterceptor[] interceptors) =>
        Build(builder => builder.UseNpgsql(connectionString), interceptors);

    public static InterfaceOnlyDcbDbContext OnSqlServer(string connectionString, TimeProvider timeProvider,
        IHttpContextAccessor httpContextAccessor) =>
        new(new DbContextOptionsBuilder<InterfaceOnlyDcbDbContext>().UseSqlServer(connectionString).Options,
            timeProvider, httpContextAccessor);

    public static InterfaceOnlyDcbDbContext OnPostgreSql(string connectionString, TimeProvider timeProvider,
        IHttpContextAccessor httpContextAccessor) =>
        new(new DbContextOptionsBuilder<InterfaceOnlyDcbDbContext>().UseNpgsql(connectionString).Options,
            timeProvider, httpContextAccessor);

    private static InterfaceOnlyDcbDbContext Build(
        Func<DbContextOptionsBuilder<InterfaceOnlyDcbDbContext>, DbContextOptionsBuilder> useProvider,
        IInterceptor[] interceptors)
    {
        var builder = new DbContextOptionsBuilder<InterfaceOnlyDcbDbContext>();
        useProvider(builder);
        builder.AddInterceptors(interceptors);

        return new InterfaceOnlyDcbDbContext(builder.Options, TimeProvider.System, new StubHttpContextAccessor());
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        optionsBuilder.AddInterceptors(new AuditInterceptor(timeProvider, httpContextAccessor));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var tagCollation = Database.ProviderName switch
        {
            "Microsoft.EntityFrameworkCore.SqlServer" => "SQL_Latin1_General_CP1_CS_AS",
            "Npgsql.EntityFrameworkCore.PostgreSQL" => "C",
            _ => null
        };

        modelBuilder.ApplyConfiguration(new DcbEventEntityConfiguration());
        modelBuilder.ApplyConfiguration(new DcbEventTagEntityConfiguration(tagCollation));
        modelBuilder.ApplyConfiguration(new DcbTagHeadEntityConfiguration(tagCollation));
        modelBuilder.ApplyConfiguration(new DcbSnapshotEntityConfiguration());
    }
}
