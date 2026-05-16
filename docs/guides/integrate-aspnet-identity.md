# Integrate with ASP.NET Core Identity

Memoria's EF Core store can share a `DbContext` with ASP.NET Core Identity, so user data and event-sourced aggregates live side by side in the same database, in the same transaction.

For the configuration surface, see [Configuration: EF Core + Identity](../reference/configuration/ef-core-identity.md).

## When to use this

- Existing ASP.NET Core Identity app, adding event sourcing without spinning up a second store.
- Your domain needs to atomically write Identity changes (e.g. a new user) alongside domain events.
- You want a single set of EF Core migrations.

If neither of these applies, the plain [`Memoria.EventSourcing.Store.EntityFrameworkCore`](../reference/configuration/ef-core.md) package is simpler.

## The shape

Your `DbContext` inherits from `IdentityDomainDbContext` instead of `DomainDbContext`. Everything else looks like a normal Memoria + EF Core setup:

```C#
public class ApplicationDbContext(
    DbContextOptions<DomainDbContext> options,
    TimeProvider timeProvider,
    IHttpContextAccessor httpContextAccessor)
    : IdentityDomainDbContext(options, timeProvider, httpContextAccessor)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }

    public DbSet<ItemEntity> Items { get; set; } = null!;
}

services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<ApplicationDbContext>();

services
    .AddScoped(sp => new DbContextOptionsBuilder<DomainDbContext>()
        .UseSqlite(connectionString)
        .UseApplicationServiceProvider(sp)
        .Options);

services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(configuration.GetConnectionString("DefaultConnection")));

services.AddMemoriaEntityFrameworkCore<ApplicationDbContext>();
```

`IdentityDomainDbContext` is `IdentityDbContext` with Memoria's event-sourcing tables added in `OnModelCreating`. Migrations cover both sets of tables.

## Combining writes in one transaction

Use the `TrackAggregate` / `TrackEventEntities` / `Save` extensions on `IDomainDbContext` to combine an event-sourced write with Identity changes:

```C#
await userManager.CreateAsync(newUser, password);

var trackAggregateResult = await dbContext.TrackAggregate(
    streamId, profileId, profile, expectedEventSequence: 0);

await dbContext.Save();
```

Both writes share the same EF Core transaction — either both commit or neither does.

## Related

- [Configuration: EF Core + Identity](../reference/configuration/ef-core-identity.md)
- [EF Core Extensions](../reference/ef-core-extensions.md)
- [Multiple aggregates per stream](multiple-aggregates-per-stream.md)
