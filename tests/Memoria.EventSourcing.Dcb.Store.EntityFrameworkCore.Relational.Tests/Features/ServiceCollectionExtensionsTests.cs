using FluentAssertions;
using Memoria.EventSourcing.Dcb.Extensions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Relational.Tests.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Relational.Tests.Features;

/// <summary>
/// Registration: the store replaces the default service that only throws.
/// </summary>
public class ServiceCollectionExtensionsTests : RelationalTestBase
{
    private ServiceCollection RegisteredServices()
    {
        var services = new ServiceCollection();
        services.AddMemoriaDcb(typeof(SeatAggregate));
        services.AddScoped<TestDbContext>(_ => CreateContext());
        services.AddMemoriaDcbEntityFrameworkCore<TestDbContext>();
        return services;
    }

    [Fact]
    public void Registration_replaces_the_default_service_rather_than_adding_beside_it()
    {
        var services = RegisteredServices();

        services.Should().ContainSingle(service => service.ServiceType == typeof(IDcbDomainService),
            "two registrations would make which one resolves depend on ordering");
    }

    [Fact]
    public void The_resolved_service_is_the_entity_framework_core_one()
    {
        using var provider = RegisteredServices().BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IDcbDomainService>()
            .Should().BeOfType<EntityFrameworkCoreDcbDomainService>();
    }

    [Fact]
    public async Task The_resolved_service_reads_and_appends_through_the_registered_context()
    {
        using var provider = RegisteredServices().BuildServiceProvider();
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IDcbDomainService>();

        var boundary = TagQuery.AnyOf(new Tag("seat", "a1"));

        var appendResult = await service.SaveEvents(
            [new TaggedEvent(new SeatReservedEvent("a1", "s7"), [new Tag("seat", "a1")])], condition: null);
        appendResult.IsSuccess.Should().BeTrue();

        var events = await service.GetEvents(boundary);
        events.Value.Should().ContainSingle();
    }

    [Fact]
    public async Task A_read_through_the_service_returns_a_failure_rather_than_throwing()
    {
        // The extension methods let a provider exception through, because used directly that is the
        // clearer signal. The service contract says otherwise, so it translates.
        using var provider = RegisteredServices().BuildServiceProvider();
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IDcbDomainService>();

        TypeBindings.EventTypeBindings = new Dictionary<string, Type>();
        await Context.SaveEvents(
            [new TaggedEvent(new SeatReservedEvent("a1", "s7"), [new Tag("seat", "a1")])], condition: null);

        var result = await service.GetEvents(TagQuery.AnyOf(new Tag("seat", "a1")));

        result.IsNotSuccess.Should().BeTrue();
        result.Failure!.Type.Should().Be(EventSourcing.StoreFailures.StorageFailureType);
    }
}
