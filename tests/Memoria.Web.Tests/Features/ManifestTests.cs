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

    /// <summary>
    /// A sentence about the service, for the sheet that opens over it: optional, and a blank one
    /// is no description rather than an empty line.
    /// </summary>
    [Theory]
    [InlineData("\"description\": \"Orders placed in the shop, one stream a customer.\",", "Orders placed in the shop, one stream a customer.")]
    [InlineData("\"description\": \"   \",", null)]
    [InlineData("", null)]
    public void Reads_an_optional_description(string declared, string? expected)
    {
        var manifest = Manifest.Parse($$"""
            { "services": [ { "name": "orders", {{declared}} "assemblies": ["Orders.dll"], "connectionString": "Orders" } ] }
            """);

        manifest.Services.Single().Description.Should().Be(expected);
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

    /// <summary>
    /// The name is whatever was written, kept as the service is shown; the address it is browsed
    /// under is made from it — letters and digits kept, everything else dropped, each run of
    /// spaces one dash, lower case.
    /// </summary>
    [Theory]
    [InlineData("orders", "orders")]
    [InlineData("Samples Streamed", "samples-streamed")]
    [InlineData("Samples  Streamed", "samples-streamed")]
    [InlineData("  Orders Team 2 ", "orders-team-2")]
    [InlineData("e-commerce", "ecommerce")]
    [InlineData("Orders (EU) / v2", "orders-eu-v2")]
    public void Keeps_the_name_and_makes_the_address_from_it(string name, string slug)
    {
        var manifest = Manifest.Parse($$"""
            { "services": [ { "name": "{{name}}", "assemblies": ["Orders.dll"], "connectionString": "Orders" } ] }
            """);

        var service = manifest.Services.Single();
        using (new AssertionScope())
        {
            service.Name.Should().Be(name);
            service.Slug.Should().Be(slug);
        }
    }

    [Theory]
    [InlineData("!!!")]
    [InlineData("- / -")]
    public void Refuses_a_name_with_no_letter_or_digit_to_make_an_address_from(string name)
    {
        var parse = () => Manifest.Parse($$"""
            { "services": [ { "name": "{{name}}", "assemblies": ["Orders.dll"], "connectionString": "Orders" } ] }
            """);

        parse.Should().Throw<InvalidDataException>().WithMessage($"*'{name}'*no letter or digit*");
    }

    /// <summary>Two names that make one address are one service twice, whatever they look like.</summary>
    [Fact]
    public void Refuses_two_names_that_make_the_same_address()
    {
        var parse = () => Manifest.Parse("""
            { "services": [
                { "name": "Samples Streamed", "assemblies": ["Orders.dll"], "connectionString": "Orders" },
                { "name": "samples   STREAMED ", "assemblies": ["More.dll"], "connectionString": "Orders" }
            ] }
            """);

        parse.Should().Throw<InvalidDataException>().WithMessage("*'samples-streamed'*twice*");
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

        parse.Should().Throw<InvalidDataException>().WithMessage("*'orders'*twice*");
    }

    /// <summary>
    /// A service's name is the address it is browsed under, so it cannot be one of the addresses
    /// the tool already answers on.
    /// </summary>
    [Theory]
    [InlineData("settings")]
    [InlineData("Preferences")]
    [InlineData("about")]
    [InlineData("forbidden")]
    [InlineData("Signed  Out")]
    [InlineData("logout")]
    public void Refuses_a_name_that_is_an_address_the_tool_already_uses(string name)
    {
        var parse = () => Manifest.Parse($$"""
            { "services": [ { "name": "{{name}}", "assemblies": ["Orders.dll"], "connectionString": "Orders" } ] }
            """);

        parse.Should().Throw<InvalidDataException>().WithMessage($"*'{name}'*address*already*");
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
