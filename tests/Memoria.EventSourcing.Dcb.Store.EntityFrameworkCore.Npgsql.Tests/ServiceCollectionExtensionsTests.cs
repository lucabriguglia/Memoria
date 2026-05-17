using FluentAssertions;
using Memoria.EventSourcing.Dcb.Extensions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Npgsql.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Npgsql.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddMemoriaDcbEntityFrameworkCoreNpgsql_replaces_any_prior_store_registration_with_NpgsqlDcbStore()
    {
        var services = new ServiceCollection();
        services.AddMemoriaDcb();
        services.AddDbContext<TestDbContext>(o => o.UseNpgsql("Host=unused"));
        services.AddScoped<IDcbDbContext>(sp => sp.GetRequiredService<TestDbContext>());

        services.AddMemoriaDcbEntityFrameworkCoreNpgsql();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IDcbStore>().Should().BeOfType<NpgsqlDcbStore>();
    }

    [Fact]
    public void AddMemoriaDcbEntityFrameworkCoreNpgsql_registers_store_as_scoped()
    {
        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(o => o.UseNpgsql("Host=unused"));
        services.AddScoped<IDcbDbContext>(sp => sp.GetRequiredService<TestDbContext>());
        services.AddMemoriaDcbEntityFrameworkCoreNpgsql();

        using var provider = services.BuildServiceProvider();
        using var s1 = provider.CreateScope();
        using var s2 = provider.CreateScope();
        var a = s1.ServiceProvider.GetRequiredService<IDcbStore>();
        var b = s2.ServiceProvider.GetRequiredService<IDcbStore>();

        a.Should().NotBeSameAs(b);
    }
}
