using FluentAssertions;
using Memoria.EventSourcing.Dcb.Extensions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddMemoriaDcbEntityFrameworkCore_RegistersEfCoreStoreInPlaceOfInMemoryDefault()
    {
        var services = new ServiceCollection();
        services.AddMemoriaDcb();
        services.AddDbContext<TestDbContext>(o => o.UseSqlite("DataSource=:memory:"));
        services.AddScoped<IDcbDbContext>(sp => sp.GetRequiredService<TestDbContext>());

        services.AddMemoriaDcbEntityFrameworkCore();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IDcbStore>();
        store.Should().BeOfType<EntityFrameworkCoreDcbStore>();
    }

    [Fact]
    public void AddMemoriaDcbEntityFrameworkCore_RegistersStoreAsScoped()
    {
        var services = new ServiceCollection();
        services.AddMemoriaDcb();
        services.AddDbContext<TestDbContext>(o => o.UseSqlite("DataSource=:memory:"));
        services.AddScoped<IDcbDbContext>(sp => sp.GetRequiredService<TestDbContext>());
        services.AddMemoriaDcbEntityFrameworkCore();

        using var provider = services.BuildServiceProvider();
        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();
        var a = scope1.ServiceProvider.GetRequiredService<IDcbStore>();
        var b = scope2.ServiceProvider.GetRequiredService<IDcbStore>();

        a.Should().NotBeSameAs(b);
    }
}
