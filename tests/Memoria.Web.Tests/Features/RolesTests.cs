using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// What a signed-in operator may do is decided by the role they hold, and the role is read off a
/// claim the provider sent, mapped by configuration. Nothing is mapped until it is said, so an
/// operator nobody mapped is a Reader: they can look, and the settings page — which takes an
/// assembly and runs it — sends them away with the name of the role they lack.
/// </summary>
public class RolesTests
{
    private const string Admins = "memoria-admins";

    [Fact]
    public async Task Sends_an_operator_mapped_to_no_role_away_from_the_upload_and_installs_nothing()
    {
        using var web = MemoriaWeb.SignedInAs("Ada Lovelace", ("roles", "memoria-readers"))
            .With("Authorization:Roles:Administrator", Admins);
        var client = web.Client;

        var response = await client.PostAsync("/settings/upload", await Upload(client));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        Forbidden(response).Should().Be(("Administrator", "/settings/upload"));
        web.Installed.Should().BeEmpty();
    }

    [Fact]
    public async Task Lets_an_operator_mapped_to_administrator_upload()
    {
        using var web = MemoriaWeb.SignedInAs("Ada Lovelace", ("roles", Admins))
            .With("Authorization:Roles:Administrator", Admins);
        var client = web.Client;

        var response = await client.PostAsync("/settings/upload", await Upload(client));

        response.Headers.Location?.ToString().Should().NotStartWith("/forbidden");
        web.Installed.Should().Contain(file => file.EndsWith("orders.zip", StringComparison.Ordinal));
    }

    /// <summary>
    /// Every provider puts its groups somewhere else. Told which claim, the tool reads that one and
    /// no longer the default — a group under the wrong claim is a group nobody mapped.
    /// </summary>
    [Theory]
    [InlineData("cognito:groups", HttpStatusCode.OK)]
    [InlineData("roles", HttpStatusCode.Found)]
    public async Task Reads_the_roles_off_the_claim_it_was_told_to(string carried, HttpStatusCode settings)
    {
        using var web = MemoriaWeb.SignedInAs("Ada Lovelace", (carried, Admins))
            .With("Authorization:RoleClaimType", "cognito:groups")
            .With("Authorization:Roles:Administrator", Admins);

        var response = await web.Client.GetAsync("/settings");

        response.StatusCode.Should().Be(settings);
    }

    [Fact]
    public async Task Shows_the_settings_link_only_to_an_administrator()
    {
        using var reader = MemoriaWeb.SignedInAs("Ada Lovelace")
            .With("Authorization:Roles:Administrator", Admins);
        using var administrator = MemoriaWeb.SignedInAs("Ada Lovelace", ("roles", Admins))
            .With("Authorization:Roles:Administrator", Admins);

        Markup.Header(await reader.Client.GetStringAsync("/")).Should().NotContain("href=\"settings\"");
        Markup.Header(await administrator.Client.GetStringAsync("/")).Should().Contain("href=\"settings\"");
    }

    [Fact]
    public async Task Names_the_operator_the_role_and_the_address_on_the_forbidden_page()
    {
        using var web = MemoriaWeb.SignedInAs("Ada Lovelace");

        var response = await web.Client.GetAsync("/forbidden?role=Administrator&returnUrl=%2Fsettings");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadAsStringAsync();
        page.Should().Contain("Ada Lovelace").And.Contain("Administrator").And.Contain("/settings");
    }

    /// <summary>
    /// Silence maps nobody, and the log says so at start-up: an administrator who forgot the
    /// mapping finds out there before they find out at the upload form.
    /// </summary>
    [Fact]
    public async Task Says_at_start_up_that_nobody_is_mapped_and_makes_everyone_a_reader()
    {
        using var web = MemoriaWeb.SignedInAs("Ada Lovelace", ("roles", Admins));
        var client = web.Client;

        var response = await client.GetAsync("/settings");

        Forbidden(response).Should().Be(("Administrator", "/settings"));
        web.Logged.Should().Contain(entry =>
            entry.Level == LogLevel.Information && entry.Message.Contains("Reader"));
    }

    [Fact]
    public async Task Applies_no_roles_at_all_when_running_open()
    {
        using var web = MemoriaWeb.Open().With("Authorization:Roles:Administrator", Admins);
        var client = web.Client;

        var response = await client.PostAsync("/settings/upload", await Upload(client, tokenPage: "/settings"));

        response.Headers.Location?.ToString().Should().NotStartWith("/forbidden");
        web.Installed.Should().Contain(file => file.EndsWith("orders.zip", StringComparison.Ordinal));
    }

    private const string Updaters = "memoria-updaters";

    /// <summary>
    /// Update writes a snapshot, and needs the Updater role — held outright, or included in
    /// Administrator. A Reader is sent away with the role's name; an Updater is let through to
    /// whatever the store then says, and is still turned away from Settings.
    /// </summary>
    [Theory]
    [InlineData("memoria-readers", true)]
    [InlineData(Updaters, false)]
    [InlineData(Admins, false)]
    public async Task Lets_only_an_updater_or_above_refresh_a_snapshot(string group, bool forbidden)
    {
        using var web = MemoriaWeb.SignedInAs("Ada Lovelace", ("roles", group))
            .With("Authorization:Roles:Administrator", Admins)
            .With("Authorization:Roles:Updater", Updaters);
        var client = web.Client;
        var page = await client.GetStringAsync("/");

        var response = await client.PostAsync("/streamed/aggregates/update", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                [Forms.AntiforgeryField] = Forms.AntiforgeryToken(page),
                ["type"] = "Nothing",
                ["stream"] = "sample:1",
                ["id"] = "sample-1:1",
                ["returnUrl"] = "/streamed/aggregates"
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        if (forbidden)
        {
            Forbidden(response).Should().Be(("Updater", "/streamed/aggregates/update"));
        }
        else
        {
            response.Headers.Location!.OriginalString.Should().NotStartWith("/forbidden");
        }
    }

    [Fact]
    public async Task Turns_an_updater_away_from_settings()
    {
        using var web = MemoriaWeb.SignedInAs("Ada Lovelace", ("roles", Updaters))
            .With("Authorization:Roles:Updater", Updaters);

        var response = await web.Client.GetAsync("/settings");

        Forbidden(response).Should().Be(("Administrator", "/settings"));
    }

    /// <summary>
    /// The tab stays where it is for everyone, since it is on a page a Reader is already reading;
    /// what changes is what it holds. A Reader is told which role the button needs, and gets no
    /// button. An Updater gets the panel as it was.
    /// </summary>
    [Theory]
    [InlineData("memoria-readers", true)]
    [InlineData(Updaters, false)]
    public async Task Tells_a_reader_on_the_update_tab_which_role_the_button_needs(string group, bool told)
    {
        using var web = MemoriaWeb.SignedInAs("Ada Lovelace", ("roles", group))
            .With("Authorization:Roles:Updater", Updaters)
            .WithSampleTypes();

        var page = await web.Client.GetStringAsync(MemoriaWeb.SampleAggregateDetail("update"));

        page.Should().Contain("id=\"tab-update\"");
        page.Contains("needs the Updater role").Should().Be(told);
        page.Contains("action=\"streamed/aggregates/update\"").Should().Be(!told,
            "the form is offered to an Updater and withheld from a Reader");
    }

    /// <summary>The role the redirect says was needed, and where the operator was going.</summary>
    private static (string Role, string ReturnUrl) Forbidden(HttpResponseMessage response)
    {
        // Relative, as the application sends it, so the query is split off by hand.
        var location = response.Headers.Location!.OriginalString;
        location.Should().StartWith("/forbidden");

        var asked = QueryHelpers.ParseQuery(location[location.IndexOf('?')..]);
        return (asked["role"].ToString(), asked["returnUrl"].ToString());
    }

    /// <summary>
    /// An upload of one archive, with the token the page would have sent. Read off the home page,
    /// whose sign-out form carries one for anyone signed in; open, there is no sign-out form, and
    /// the settings page's own forms are the ones that carry it.
    /// </summary>
    private static async Task<MultipartFormDataContent> Upload(HttpClient client, string tokenPage = "/")
    {
        var page = await client.GetStringAsync(tokenPage);

        return new MultipartFormDataContent
        {
            { new StringContent(Forms.AntiforgeryToken(page)), Forms.AntiforgeryField },
            { new ByteArrayContent(Forms.Zip("Contoso.Orders.dll")), "files", "orders.zip" }
        };
    }
}
