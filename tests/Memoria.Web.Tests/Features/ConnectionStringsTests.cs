using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Memoria.Web.Data;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// The connection strings the tool has, by name: a service names one, and the configuration holds
/// it. Which engine opens each is read off the string, or from a setting under that name where
/// the string could be more than one — <c>Databases:{name}:Provider</c> — with the one string the
/// tool has always read, <c>Memoria</c>, keeping its older <c>Database:Provider</c> as well.
/// </summary>
public class ConnectionStringsTests
{
    private const string Postgres = "Host=localhost;Database=orders;Username=postgres;Password=x";

    private const string Sqlite = "Data Source=billing.db";

    /// <summary>A string every relational provider would take, so nothing in it says which.</summary>
    private const string Ambiguous = "Server=db;Database=orders;User Id=u;Password=p";

    private static IConfiguration Configuration(params (string Key, string Value)[] settings) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(setting =>
                new KeyValuePair<string, string?>(setting.Key, setting.Value)))
            .Build();

    [Fact]
    public void Reads_each_named_string_with_its_own_provider()
    {
        var configuration = Configuration(("ConnectionStrings:Orders", Postgres), ("ConnectionStrings:Billing", Sqlite));

        var orders = ConnectionStrings.Named(configuration, "Orders");
        var billing = ConnectionStrings.Named(configuration, "Billing");

        using (new AssertionScope())
        {
            orders.Database!.Provider.Should().Be(DatabaseProvider.Npgsql);
            billing.Database!.Provider.Should().Be(DatabaseProvider.Sqlite);
        }
    }

    [Fact]
    public void Settles_an_ambiguous_string_from_the_setting_under_its_own_name_only()
    {
        var configuration = Configuration(
            ("ConnectionStrings:Orders", Ambiguous), ("ConnectionStrings:Billing", Ambiguous),
            ("Databases:Orders:Provider", "SqlServer"));

        var orders = ConnectionStrings.Named(configuration, "Orders");
        var billing = ConnectionStrings.Named(configuration, "Billing");

        using (new AssertionScope())
        {
            orders.Database!.Provider.Should().Be(DatabaseProvider.SqlServer);
            billing.Database.Should().BeNull();
            billing.Problem.Should().Contain("Databases:Billing:Provider");
        }
    }

    [Fact]
    public void Still_settles_the_memoria_string_from_the_older_setting()
    {
        var configuration = Configuration(("ConnectionStrings:Memoria", Ambiguous), ("Database:Provider", "Npgsql"));

        ConnectionStrings.Named(configuration, "Memoria").Database!.Provider.Should().Be(DatabaseProvider.Npgsql);
    }

    [Fact]
    public void Says_when_a_name_is_not_configured_rather_than_throwing()
    {
        var named = ConnectionStrings.Named(Configuration(), "Orders");

        using (new AssertionScope())
        {
            named.Configured.Should().BeFalse();
            named.Database.Should().BeNull();
            named.Problem.Should().BeNull();
        }
    }

    [Fact]
    public void Says_when_a_configured_string_cannot_be_read_rather_than_throwing()
    {
        var named = ConnectionStrings.Named(Configuration(("ConnectionStrings:Orders", "nonsense")), "Orders");

        using (new AssertionScope())
        {
            named.Configured.Should().BeTrue();
            named.Database.Should().BeNull();
            named.Problem.Should().Contain("could not be read");
        }
    }

    /// <summary>
    /// What start-up checks: every string that is there must be readable, whatever it is called,
    /// since a string nobody can open is a mistake best found before a page asks for it.
    /// </summary>
    [Fact]
    public void Refuses_at_start_up_a_configured_string_that_cannot_be_read()
    {
        var validate = () => ConnectionStrings.Validate(Configuration(
            ("ConnectionStrings:Orders", Postgres), ("ConnectionStrings:Billing", "nonsense")));

        validate.Should().Throw<InvalidOperationException>().WithMessage("*Billing*could not be read*");
    }

    [Fact]
    public void Accepts_at_start_up_no_strings_at_all()
    {
        var validate = () => ConnectionStrings.Validate(Configuration());

        validate.Should().NotThrow();
    }

    [Fact]
    public void Reads_the_cosmos_names_from_the_setting_under_its_own_name()
    {
        var cosmos = "AccountEndpoint=https://localhost:8081/;AccountKey=a2V5";
        var configuration = Configuration(
            ("ConnectionStrings:Orders", cosmos),
            ("Databases:Orders:Cosmos:DatabaseName", "OrdersDb"), ("Databases:Orders:Cosmos:ContainerName", "Events"));

        var orders = ConnectionStrings.Named(configuration, "Orders");

        using (new AssertionScope())
        {
            orders.Database!.Provider.Should().Be(DatabaseProvider.Cosmos);
            orders.Cosmos!.DatabaseName.Should().Be("OrdersDb");
            orders.Cosmos.ContainerName.Should().Be("Events");
        }
    }

    [Fact]
    public void Still_reads_the_memoria_cosmos_names_from_the_older_settings()
    {
        var cosmos = "AccountEndpoint=https://localhost:8081/;AccountKey=a2V5";
        var configuration = Configuration(
            ("ConnectionStrings:Memoria", cosmos), ("Database:Cosmos:DatabaseName", "Old"), ("Database:Cosmos:ContainerName", "Rows"));

        var memoria = ConnectionStrings.Named(configuration, "Memoria");

        using (new AssertionScope())
        {
            memoria.Cosmos!.DatabaseName.Should().Be("Old");
            memoria.Cosmos.ContainerName.Should().Be("Rows");
        }
    }
}
