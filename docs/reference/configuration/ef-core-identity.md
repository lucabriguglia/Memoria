# Entity Framework Core + ASP.NET Core Identity

Memoria supports `IdentityDbContext` from ASP.NET Core Identity, letting you integrate event sourcing with user management and authentication features.

## Registration

Install the **Memoria.EventSourcing.Store.EntityFrameworkCore.Identity** package and use `IdentityDomainDbContext` in your application:

```C#
// Your db context that inherits from IdentityDomainDbContext
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

// Register identity
services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Register the db context with the provider of your choice
services
    .AddScoped(sp => new DbContextOptionsBuilder<DomainDbContext>()
        .UseSqlite(connectionString)
        .UseApplicationServiceProvider(sp)
        .Options);

services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(configuration.GetConnectionString("DefaultConnection")));

// Register the event sourcing store provider
services.AddMemoriaEntityFrameworkCore<ApplicationDbContext>();
```

## Related

- [Entity Framework Core](ef-core.md)
- [Domain Service](../domain-service.md)
