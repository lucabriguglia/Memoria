using FluentAssertions;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Extensions;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Filtering;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Tests.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Tests.Features.Filtering;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void Registers_substring_filter_as_default()
    {
        var services = BuildBaseServices("filter-registration");

        services.AddMemoriaEntityFrameworkCore<TestDbContext>();

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IEventDataFilter>().Should().BeOfType<SubstringEventDataFilter>();
    }

    [Fact]
    public void Honours_a_pre_registered_filter()
    {
        var services = BuildBaseServices("filter-pre-registered");
        var custom = new StubEventDataFilter();
        services.AddSingleton<IEventDataFilter>(custom);

        services.AddMemoriaEntityFrameworkCore<TestDbContext>();

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IEventDataFilter>().Should().BeSameAs(custom);
    }

    private static ServiceCollection BuildBaseServices(string databaseName)
    {
        var services = new ServiceCollection();
        services.AddScoped(sp => new DbContextOptionsBuilder<DomainDbContext>()
            .UseInMemoryDatabase(databaseName)
            .UseApplicationServiceProvider(sp)
            .Options);
        services.AddDbContext<TestDbContext>(options => options.UseInMemoryDatabase(databaseName));
        return services;
    }

    private sealed class StubEventDataFilter : IEventDataFilter
    {
        public IQueryable<EventEntity> ApplyPropertyFilter(IQueryable<EventEntity> query, string propertyName, string propertyValue)
            => query;
    }
}
