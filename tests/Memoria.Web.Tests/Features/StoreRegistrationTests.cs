using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Memoria.EventSourcing;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;
using Memoria.EventSourcing.Store.Cosmos;
using Memoria.EventSourcing.Store.EntityFrameworkCore;
using Memoria.Web.Data;
using Memoria.Web.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// What a request under a service resolves: the readers, contexts and domain services built over
/// the store that service's connection string names — relational through Entity Framework Core,
/// Cosmos through its own client — and what the pages under it may offer.
/// </summary>
public class StoreRegistrationTests : IDisposable
{
    private const string Postgres = "Host=localhost;Database=memoria;Username=postgres;Password=x";

    private const string Cosmos = "AccountEndpoint=https://localhost:8081/;AccountKey=a2V5";

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"memoria-web-{Guid.NewGuid():N}");

    private static IConfiguration Configuration(params (string Key, string Value)[] settings) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(setting =>
                new KeyValuePair<string, string?>(setting.Key, setting.Value)))
            .Build();

    private static Service Orders => new("Orders", ["Orders.dll"], "Orders", [], []);

    /// <summary>
    /// A container wired the way the application wires it, with the Orders service declared and
    /// its connection string configured as given, and one request scope entered into it.
    /// </summary>
    private IServiceScope Under(string connectionString, params (string Key, string Value)[] settings)
    {
        var configuration = Configuration([("ConnectionStrings:Orders", connectionString), .. settings]);
        var services = new ServiceCollection();

        services.AddSingleton(configuration);
        services.AddDomainExtensions(new ExtensionStore(_root));
        services.AddStores();

        var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<CurrentService>().Enter(Orders, DomainTypeCatalogue.Empty);

        return scope;
    }

    [Fact]
    public void Reads_a_relational_store_through_entity_framework_core()
    {
        using var scope = Under(Postgres);

        using (new AssertionScope())
        {
            scope.ServiceProvider.GetRequiredService<IStreamedReads>().Should().BeOfType<EfStreamedReads>();
            scope.ServiceProvider.GetRequiredService<IDomainService>().Should().BeOfType<EntityFrameworkCoreDomainService>();
            scope.ServiceProvider.GetRequiredService<IDcbDbContext>().Should().BeOfType<DcbStoreDbContext>(
                "a relational store carries the dynamic consistency boundary tables too");
        }
    }

    [Fact]
    public void Reads_a_cosmos_store_through_its_own_client_and_writes_through_the_same()
    {
        using var scope = Under(Cosmos);

        using (new AssertionScope())
        {
            scope.ServiceProvider.GetRequiredService<IStreamedReads>().Should().BeOfType<CosmosStreamedReads>();
            scope.ServiceProvider.GetRequiredService<IDomainService>().Should().BeOfType<CosmosDomainService>(
                "the update tab sends the write through the Cosmos domain service");
        }
    }

    /// <summary>
    /// The context a request under a service resolves reads through that service's bindings, the
    /// set the registry built for it — never the process-wide one.
    /// </summary>
    [Fact]
    public void Builds_a_context_reading_through_the_service_s_own_bindings()
    {
        using var scope = Under("Data Source=memoria.db");

        var context = scope.ServiceProvider.GetRequiredService<StreamedStoreDbContext>();

        context.TypeBindings.Should().BeSameAs(
            scope.ServiceProvider.GetRequiredService<ServiceStore>().Bindings);
    }

    /// <summary>
    /// What the tool can offer depends on the store behind the service. Only the dynamic
    /// consistency boundary differs today: there is no DCB store for Cosmos, so those pages have
    /// nothing to read and are not offered rather than offered and broken.
    /// </summary>
    [Theory]
    [InlineData(Postgres, true)]
    [InlineData("Server=.;Database=memoria;Trusted_Connection=True", true)]
    [InlineData("Data Source=memoria.db", true)]
    [InlineData(Cosmos, false)]
    public void Offers_the_dynamic_consistency_boundary_only_where_there_is_a_store_for_it(
        string connectionString, bool expected)
    {
        using var scope = Under(connectionString);

        scope.ServiceProvider.GetRequiredService<StoreCapabilities>().HasDcb.Should().Be(expected);
    }

    [Theory]
    [InlineData(Postgres, true)]
    [InlineData("Data Source=memoria.db", true)]
    [InlineData(Cosmos, true)]
    public void Offers_the_update_wherever_a_store_can_be_written_to(string connectionString, bool expected)
    {
        using var scope = Under(connectionString);

        scope.ServiceProvider.GetRequiredService<StoreCapabilities>().CanUpdate.Should().Be(expected);
    }

    /// <summary>A service whose string is not there has nothing to build a reader over, and says so.</summary>
    [Fact]
    public void Says_why_a_service_over_an_unconfigured_string_resolves_no_reader()
    {
        using var scope = Under("");

        var store = scope.ServiceProvider.GetRequiredService<ServiceStore>();
        var resolve = () => scope.ServiceProvider.GetRequiredService<IStreamedReads>();

        using (new AssertionScope())
        {
            store.Problem.Should().Contain("Orders").And.Contain("not configured");
            store.Capabilities.HasDcb.Should().BeFalse();
            resolve.Should().Throw<InvalidOperationException>().WithMessage("*Orders*not configured*");
        }
    }

    /// <summary>Outside every service there is no store to resolve, and asking is a mistake the message names.</summary>
    [Fact]
    public void Resolves_no_store_outside_a_service()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Configuration());
        services.AddDomainExtensions(new ExtensionStore(_root));
        services.AddStores();
        using var scope = services.BuildServiceProvider().CreateScope();

        var resolve = () => scope.ServiceProvider.GetRequiredService<IStreamedReads>();

        resolve.Should().Throw<InvalidOperationException>().WithMessage("*under no service*");
    }

    /// <summary>
    /// A Cosmos connection string names an account, not a database or a container, so those two
    /// are configuration — under the string's own name, with the one string the tool has always
    /// read keeping its older unnamed settings. They default to what the store's own options
    /// default to, so a store installed with those defaults needs no settings at all.
    /// </summary>
    [Fact]
    public void Defaults_the_cosmos_database_and_container_to_the_store_s_own_defaults()
    {
        var connection = DatabaseConnection.Of(Cosmos, configured: null);

        var store = CosmosStore.Of("Orders", connection, Configuration());

        using (new AssertionScope())
        {
            store.DatabaseName.Should().Be("Memoria");
            store.ContainerName.Should().Be("Domain");
        }
    }

    [Fact]
    public void Takes_the_cosmos_database_and_container_configured_under_the_string_s_own_name()
    {
        var connection = DatabaseConnection.Of(Cosmos, configured: null);

        var store = CosmosStore.Of("Orders", connection, Configuration(
            ("Databases:Orders:Cosmos:DatabaseName", "orders"),
            ("Databases:Orders:Cosmos:ContainerName", "events"),
            ("Database:Cosmos:DatabaseName", "not-this-one")));

        using (new AssertionScope())
        {
            store.DatabaseName.Should().Be("orders");
            store.ContainerName.Should().Be("events");
        }
    }

    [Fact]
    public void Keeps_the_older_cosmos_settings_for_the_memoria_string()
    {
        var connection = DatabaseConnection.Of(Cosmos, configured: null);

        var store = CosmosStore.Of("Memoria", connection, Configuration(
            ("Database:Cosmos:DatabaseName", "orders"),
            ("Database:Cosmos:ContainerName", "events")));

        using (new AssertionScope())
        {
            store.DatabaseName.Should().Be("orders");
            store.ContainerName.Should().Be("events");
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        GC.SuppressFinalize(this);
    }
}
