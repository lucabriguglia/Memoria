using FluentAssertions;
using Memoria.EventSourcing.Dcb.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Memoria.EventSourcing.Dcb.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddMemoriaDcb_RegistersInMemoryStoreAsDefault()
    {
        var services = new ServiceCollection();

        services.AddMemoriaDcb();

        using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IDcbStore>();
        store.Should().BeOfType<InMemoryDcbStore>();
    }

    [Fact]
    public void AddMemoriaDcb_RegistersStoreAsSingleton_SoInMemoryStateIsShared()
    {
        var services = new ServiceCollection();

        services.AddMemoriaDcb();

        using var provider = services.BuildServiceProvider();
        var a = provider.GetRequiredService<IDcbStore>();
        var b = provider.GetRequiredService<IDcbStore>();
        a.Should().BeSameAs(b);
    }
}
