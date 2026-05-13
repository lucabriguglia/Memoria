using FluentAssertions;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Filtering;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Npgsql.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void Replaces_any_existing_event_data_filter()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventDataFilter, SubstringEventDataFilter>();

        services.AddMemoriaEntityFrameworkCoreNpgsql();

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IEventDataFilter>().Should().BeOfType<NpgsqlJsonEventDataFilter>();
    }

    [Fact]
    public void Registers_filter_even_when_none_was_present()
    {
        var services = new ServiceCollection();

        services.AddMemoriaEntityFrameworkCoreNpgsql();

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IEventDataFilter>().Should().BeOfType<NpgsqlJsonEventDataFilter>();
    }
}
