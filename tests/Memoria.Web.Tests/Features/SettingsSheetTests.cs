using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// A zip must carry a manifest, and the Installed table opens a sheet over each archive: the file,
/// and each service the manifest declares — its name, the assemblies it names with the types
/// registered from each, the connection string it reads over and whether that is configured, and
/// who may read and update it. An archive with no usable manifest is listed and says why.
/// </summary>
public class SettingsSheetTests
{
    private const string Sheet = "/settings?tab=installed&service=orders";

    [Fact]
    public async Task Refuses_an_upload_without_a_manifest_and_installs_nothing()
    {
        using var web = MemoriaWeb.Open();
        var client = web.Client;

        var response = await client.PostAsync("/settings/upload",
            await Upload(client, Forms.ZipWithoutManifest("Contoso.Orders.dll")));

        using (new AssertionScope())
        {
            response.StatusCode.Should().Be(HttpStatusCode.Found);
            Error(response).Should().Contain("orders.zip could not be installed").And.Contain("memoria.json");
            web.Installed.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task Refuses_a_manifest_naming_an_assembly_the_zip_does_not_carry()
    {
        using var web = MemoriaWeb.Open();
        var client = web.Client;
        var manifest = """
            { "services": [ { "name": "orders", "assemblies": ["Missing.dll"], "connectionString": "Memoria" } ] }
            """;

        var response = await client.PostAsync("/settings/upload",
            await Upload(client, Forms.Zip("Contoso.Orders.dll", manifest)));

        using (new AssertionScope())
        {
            Error(response).Should().Contain("Missing.dll").And.Contain("not in the archive");
            web.Installed.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task Installs_an_archive_with_a_manifest_and_lists_it()
    {
        using var web = MemoriaWeb.Open();
        var client = web.Client;

        var response = await client.PostAsync("/settings/upload", await Upload(client, Forms.Zip("Contoso.Orders.dll")));
        var page = Markup.Plain(await client.GetStringAsync("/settings?tab=installed"));

        using (new AssertionScope())
        {
            response.Headers.Location?.ToString().Should().Contain("message=");
            page.Should().Contain("orders.zip");
        }
    }

    /// <summary>
    /// Each service in the row is the way into its own sheet, one to a line and marked as a place
    /// to go; the row itself leads nowhere.
    /// </summary>
    [Fact]
    public async Task Lists_each_service_on_its_own_line_as_a_link_that_opens_its_sheet()
    {
        using var web = MemoriaWeb.Open();
        var client = web.Client;
        var manifest = """
            { "services": [
                { "name": "Orders", "assemblies": ["Contoso.Orders.dll"], "connectionString": "Memoria" },
                { "name": "Billing", "assemblies": ["Contoso.Orders.dll"], "connectionString": "Memoria" }
            ] }
            """;
        await client.PostAsync("/settings/upload", await Upload(client, Forms.Zip("Contoso.Orders.dll", manifest)));

        var page = Markup.Plain(await client.GetStringAsync("/settings?tab=installed"));

        using (new AssertionScope())
        {
            page.Should().NotContain("class=\"clickable\"");
            page.Should().NotContain("archive=orders.zip");
            Markup.ServiceLinks(page).Should().Equal(
                ("settings?tab=installed&amp;service=orders#service", "Orders"),
                ("settings?tab=installed&amp;service=billing#service", "Billing"));
        }
    }

    /// <summary>
    /// The sheet opens on the service's facts: where it is browsed, what it reads over, and who
    /// may read and update it. The types its assemblies registered are a second view.
    /// </summary>
    [Fact]
    public async Task Opens_a_sheet_over_a_service_with_its_facts_and_its_types_as_two_views()
    {
        using var web = MemoriaWeb.Open();
        var client = web.Client;
        await client.PostAsync("/settings/upload", await Upload(client,
            Forms.Zip("Contoso.Orders.dll", readRoles: ["orders-team"], updateRoles: ["orders-leads"])));

        var info = Markup.Plain(await client.GetStringAsync(Sheet));
        var types = Markup.Plain(await client.GetStringAsync(Sheet + "&view=types"));

        using (new AssertionScope())
        {
            info.Should().Contain("id=\"service\"");
            info.Should().Contain("id=\"service-title\"");
            info.Should().Contain("aria-current=\"page\">Info<").And.NotContain("aria-current=\"page\">Types<");
            types.Should().Contain("aria-current=\"page\">Types<");
            info.Should().Contain("/orders");
            info.Should().Contain("orders-team").And.Contain("orders-leads");
            info.Should().NotContain("Contoso.Orders.dll");
            info.Should().Contain("href=\"settings?tab=installed&amp;service=orders&amp;view=types#service\"");
            types.Should().Contain("Contoso.Orders.dll").And.NotContain("orders-team");
        }
    }

    /// <summary>
    /// Every instance is given a SQLite store under <c>Memoria</c>, so a service naming that reads
    /// over a configured string and the sheet says which engine opens it; one naming a string the
    /// configuration lacks is told so, since a manifest cannot check that and the person reading
    /// the sheet is the one who can fix it.
    /// </summary>
    [Fact]
    public async Task Says_whether_each_service_s_connection_string_is_configured()
    {
        using var web = MemoriaWeb.Open();
        var client = web.Client;
        var manifest = """
            { "services": [
                { "name": "orders", "assemblies": ["Contoso.Orders.dll"], "connectionString": "Memoria" },
                { "name": "billing", "assemblies": ["Contoso.Orders.dll"], "connectionString": "Billing" }
            ] }
            """;
        await client.PostAsync("/settings/upload", await Upload(client, Forms.Zip("Contoso.Orders.dll", manifest)));

        var orders = Markup.Plain(await client.GetStringAsync(Sheet));
        var billing = Markup.Plain(await client.GetStringAsync("/settings?tab=installed&service=billing"));

        using (new AssertionScope())
        {
            orders.Should().Contain("Memoria").And.Contain("SQLite");
            billing.Should().Contain("Billing").And.Contain("not configured");
        }
    }

    /// <summary>
    /// What the manifest says the service is, on the sheet's facts when it says anything; the
    /// row is not drawn at all when it does not, rather than left blank.
    /// </summary>
    [Fact]
    public async Task Shows_the_service_s_description_on_its_facts_when_the_manifest_gives_one()
    {
        using var web = MemoriaWeb.Open();
        var client = web.Client;
        await client.PostAsync("/settings/upload", await Upload(client,
            Forms.Zip("Contoso.Orders.dll", description: "Orders placed in the shop, one stream a customer.")));
        await client.PostAsync("/settings/upload", await Upload(client,
            Forms.Zip("Contoso.Billing.dll", service: "billing"), file: "billing.zip"));

        var described = Markup.Plain(await client.GetStringAsync(Sheet));
        var plain = Markup.Plain(await client.GetStringAsync("/settings?tab=installed&service=billing"));

        using (new AssertionScope())
        {
            described.Should().Contain("Orders placed in the shop, one stream a customer.");
            plain.Should().NotContain("Description");
        }
    }

    [Fact]
    public async Task Says_when_a_service_declares_no_roles()
    {
        using var web = MemoriaWeb.Open();
        var client = web.Client;
        await client.PostAsync("/settings/upload", await Upload(client, Forms.Zip("Contoso.Orders.dll")));

        var sheet = Markup.Plain(await client.GetStringAsync(Sheet));

        sheet.Should().Contain("global roles");
    }

    /// <summary>
    /// A zip put in the directory by hand, before manifests were required, is listed rather than
    /// hidden — the row says it needs a manifest and why, where its services would be, and it
    /// registers nothing.
    /// </summary>
    [Fact]
    public async Task Lists_an_archive_without_a_manifest_and_says_it_needs_one()
    {
        using var web = MemoriaWeb.Open();
        Directory.CreateDirectory(Path.Combine(web.ExtensionsDirectory, "zips"));
        await File.WriteAllBytesAsync(Path.Combine(web.ExtensionsDirectory, "zips", "old.zip"),
            Forms.ZipWithoutManifest("Contoso.Old.dll"));
        var client = web.Client;

        var page = Markup.Plain(await client.GetStringAsync("/settings?tab=installed"));

        page.Should().Contain("old.zip").And.Contain("No manifest")
            .And.Contain("memoria.json").And.Contain("required");
    }

    [Fact]
    public async Task Opens_no_sheet_for_a_service_that_is_not_installed()
    {
        using var web = MemoriaWeb.Open();
        var client = web.Client;

        var page = Markup.Plain(await client.GetStringAsync("/settings?tab=installed&service=nothing"));

        page.Should().NotContain("id=\"service\"");
    }

    private static async Task<MultipartFormDataContent> Upload(HttpClient client, byte[] zip, string file = "orders.zip")
    {
        var page = await client.GetStringAsync("/settings");

        return new MultipartFormDataContent
        {
            { new StringContent(Forms.AntiforgeryToken(page)), Forms.AntiforgeryField },
            { new ByteArrayContent(zip), "files", file }
        };
    }

    /// <summary>The error the redirect carries back to the settings page.</summary>
    private static string Error(HttpResponseMessage response)
    {
        var location = response.Headers.Location?.ToString() ?? string.Empty;
        var query = QueryHelpers.ParseQuery(new Uri(location, UriKind.RelativeOrAbsolute).IsAbsoluteUri
            ? new Uri(location).Query
            : location[location.IndexOf('?')..]);

        return query.TryGetValue("error", out var error) ? error.ToString() : string.Empty;
    }
}
