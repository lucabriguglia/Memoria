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
    private const string Sheet = "/settings?tab=installed&archive=orders.zip";

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

    /// <summary>The row itself is the way into the sheet, not a mark somewhere on it.</summary>
    [Fact]
    public async Task Lists_each_archive_as_a_row_that_opens_its_sheet()
    {
        using var web = MemoriaWeb.Open();
        var client = web.Client;
        await client.PostAsync("/settings/upload", await Upload(client, Forms.Zip("Contoso.Orders.dll")));

        var page = Markup.Plain(await client.GetStringAsync("/settings?tab=installed"));

        page.Should().Contain("href=\"settings?tab=installed&amp;archive=orders.zip#archive\"");
    }

    [Fact]
    public async Task Opens_a_sheet_naming_the_file_and_each_service_it_declares()
    {
        using var web = MemoriaWeb.Open();
        var client = web.Client;
        await client.PostAsync("/settings/upload", await Upload(client,
            Forms.Zip("Contoso.Orders.dll", readRoles: ["orders-team"], updateRoles: ["orders-leads"])));

        var sheet = Markup.Plain(await client.GetStringAsync(Sheet));

        using (new AssertionScope())
        {
            sheet.Should().Contain("id=\"archive\"");
            sheet.Should().Contain("orders.zip");
            sheet.Should().Contain("Uploaded");
            sheet.Should().Contain(">orders<");
            sheet.Should().Contain("Contoso.Orders.dll");
            sheet.Should().Contain("orders-team");
            sheet.Should().Contain("orders-leads");
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

        var sheet = Markup.Plain(await client.GetStringAsync(Sheet));

        using (new AssertionScope())
        {
            sheet.Should().Contain("Memoria").And.Contain("SQLite");
            sheet.Should().Contain("Billing").And.Contain("not configured");
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
    /// hidden — the row says it needs a manifest, registers nothing, and its sheet says why.
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
        var sheet = Markup.Plain(await client.GetStringAsync("/settings?tab=installed&archive=old.zip"));

        using (new AssertionScope())
        {
            page.Should().Contain("old.zip").And.Contain("No manifest");
            sheet.Should().Contain("memoria.json").And.Contain("required");
        }
    }

    [Fact]
    public async Task Opens_no_sheet_for_a_name_that_is_not_installed()
    {
        using var web = MemoriaWeb.Open();
        var client = web.Client;

        var page = Markup.Plain(await client.GetStringAsync("/settings?tab=installed&archive=nothing.zip"));

        page.Should().NotContain("id=\"archive\"");
    }

    private static async Task<MultipartFormDataContent> Upload(HttpClient client, byte[] zip)
    {
        var page = await client.GetStringAsync("/settings");

        return new MultipartFormDataContent
        {
            { new StringContent(Forms.AntiforgeryToken(page)), Forms.AntiforgeryField },
            { new ByteArrayContent(zip), "files", "orders.zip" }
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
