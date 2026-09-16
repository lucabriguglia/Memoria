using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Memoria.Web.Extensibility;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// The manifest a zip must carry, <c>memoria.json</c>, read at a trust boundary: parsed, then held
/// to its rules, and refused with the rule it broke rather than half-read. What it declares is the
/// services the archive brings — each a name, the assemblies its types are read from, the name of
/// the connection string it is read over, and who may read and update it.
/// </summary>
public class ManifestTests
{
    private const string Orders = """
        {
          "services": [
            {
              "name": "orders",
              "assemblies": ["Contoso.Orders.Domain.dll", "Contoso.Orders.Contracts.dll"],
              "connectionString": "Orders",
              "roles": { "read": ["orders-team"], "update": ["orders-leads"] }
            }
          ]
        }
        """;

    [Fact]
    public void Reads_a_service_with_everything_declared()
    {
        var manifest = Manifest.Parse(Orders);

        var service = manifest.Services.Should().ContainSingle().Which;
        using (new AssertionScope())
        {
            service.Name.Should().Be("orders");
            service.Assemblies.Should().Equal("Contoso.Orders.Domain.dll", "Contoso.Orders.Contracts.dll");
            service.ConnectionString.Should().Be("Orders");
            service.ReadRoles.Should().Equal("orders-team");
            service.UpdateRoles.Should().Equal("orders-leads");
        }
    }

    [Fact]
    public void Reads_more_than_one_service()
    {
        var manifest = Manifest.Parse("""
            { "services": [
                { "name": "orders", "assemblies": ["Orders.dll"], "connectionString": "Orders" },
                { "name": "billing", "assemblies": ["Billing.dll"], "connectionString": "Billing" }
            ] }
            """);

        manifest.Services.Select(service => service.Name).Should().Equal("orders", "billing");
    }

    [Fact]
    public void Leaves_the_roles_empty_when_none_are_declared()
    {
        var manifest = Manifest.Parse("""
            { "services": [ { "name": "orders", "assemblies": ["Orders.dll"], "connectionString": "Orders" } ] }
            """);

        var service = manifest.Services.Single();
        using (new AssertionScope())
        {
            service.ReadRoles.Should().BeEmpty();
            service.UpdateRoles.Should().BeEmpty();
        }
    }

    [Fact]
    public void Leaves_a_role_list_empty_when_only_the_other_is_declared()
    {
        var manifest = Manifest.Parse("""
            { "services": [ { "name": "orders", "assemblies": ["Orders.dll"], "connectionString": "Orders",
                              "roles": { "update": ["orders-leads"] } } ] }
            """);

        var service = manifest.Services.Single();
        using (new AssertionScope())
        {
            service.ReadRoles.Should().BeEmpty();
            service.UpdateRoles.Should().Equal("orders-leads");
        }
    }

    [Fact]
    public void Ignores_a_key_it_does_not_know()
    {
        var manifest = Manifest.Parse("""
            { "version": 1, "services": [ { "name": "orders", "assemblies": ["Orders.dll"],
              "connectionString": "Orders", "description": "later" } ] }
            """);

        manifest.Services.Should().ContainSingle();
    }

    [Theory]
    [InlineData("orders")]
    [InlineData("Orders-2")]
    [InlineData("a")]
    public void Accepts_a_name_of_letters_digits_and_hyphens(string name)
    {
        var manifest = Manifest.Parse($$"""
            { "services": [ { "name": "{{name}}", "assemblies": ["Orders.dll"], "connectionString": "Orders" } ] }
            """);

        manifest.Services.Single().Name.Should().Be(name);
    }

    [Theory]
    [InlineData("orders 2")]
    [InlineData("orders/2")]
    [InlineData("orders.2")]
    public void Refuses_a_name_that_is_not_letters_digits_and_hyphens(string name)
    {
        var parse = () => Manifest.Parse($$"""
            { "services": [ { "name": "{{name}}", "assemblies": ["Orders.dll"], "connectionString": "Orders" } ] }
            """);

        parse.Should().Throw<InvalidDataException>().WithMessage("*name*letters, digits and hyphens*");
    }

    [Theory]
    [InlineData("""{ "services": [ { "assemblies": ["Orders.dll"], "connectionString": "Orders" } ] }""")]
    [InlineData("""{ "services": [ { "name": "", "assemblies": ["Orders.dll"], "connectionString": "Orders" } ] }""")]
    [InlineData("""{ "services": [ { "name": "   ", "assemblies": ["Orders.dll"], "connectionString": "Orders" } ] }""")]
    public void Refuses_a_service_with_no_name(string json)
    {
        var parse = () => Manifest.Parse(json);

        parse.Should().Throw<InvalidDataException>().WithMessage("*no name*");
    }

    [Fact]
    public void Refuses_a_name_declared_twice_whatever_its_case()
    {
        var parse = () => Manifest.Parse("""
            { "services": [
                { "name": "orders", "assemblies": ["Orders.dll"], "connectionString": "Orders" },
                { "name": "Orders", "assemblies": ["More.dll"], "connectionString": "Orders" }
            ] }
            """);

        parse.Should().Throw<InvalidDataException>().WithMessage("*'Orders'*twice*");
    }

    [Theory]
    [InlineData("""{ "services": [ { "name": "orders", "connectionString": "Orders" } ] }""")]
    [InlineData("""{ "services": [ { "name": "orders", "assemblies": [], "connectionString": "Orders" } ] }""")]
    public void Refuses_a_service_naming_no_assembly(string json)
    {
        var parse = () => Manifest.Parse(json);

        parse.Should().Throw<InvalidDataException>().WithMessage("*'orders'*no assemblies*");
    }

    [Theory]
    [InlineData("""{ "services": [ { "name": "orders", "assemblies": ["Orders.dll"] } ] }""")]
    [InlineData("""{ "services": [ { "name": "orders", "assemblies": ["Orders.dll"], "connectionString": "" } ] }""")]
    public void Refuses_a_service_naming_no_connection_string(string json)
    {
        var parse = () => Manifest.Parse(json);

        parse.Should().Throw<InvalidDataException>().WithMessage("*'orders'*no connection string*");
    }

    [Theory]
    [InlineData("""{ }""")]
    [InlineData("""{ "services": [] }""")]
    public void Refuses_a_manifest_declaring_no_service(string json)
    {
        var parse = () => Manifest.Parse(json);

        parse.Should().Throw<InvalidDataException>().WithMessage("*declares no services*");
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("[]")]
    public void Refuses_what_is_not_a_manifest_at_all(string json)
    {
        var parse = () => Manifest.Parse(json);

        parse.Should().Throw<InvalidDataException>().WithMessage("*could not be read*");
    }

    /// <summary>
    /// The file this is read from, by the name the archive must carry it under, at the root.
    /// </summary>
    [Fact]
    public void Is_read_from_a_file_of_a_fixed_name_at_the_archive_root()
    {
        Manifest.FileName.Should().Be("memoria.json");
    }
}
