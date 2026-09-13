using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// Nothing answers an operator who has not signed in. A page asked for without a session is not
/// the page but a redirect to the provider, and what that redirect asks for is the authorization
/// code flow with a proof key — never a token in the front channel.
/// </summary>
public class SignInTests
{
    [Fact]
    public async Task Sends_an_anonymous_page_request_to_the_provider()
    {
        using var web = MemoriaWeb.SigningIn();

        var response = await web.Client.GetAsync("/streamed");

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        var location = response.Headers.Location!;
        location.GetLeftPart(UriPartial.Path).Should().Be(MemoriaWeb.Provider.AuthorizeEndpoint);

        var asked = QueryHelpers.ParseQuery(location.Query);
        asked["response_type"].ToString().Should().Be("code");
        asked["code_challenge_method"].ToString().Should().Be("S256");
        asked["client_id"].ToString().Should().Be(MemoriaWeb.Provider.ClientId);
        asked["scope"].ToString().Should().Contain("openid");
        asked.Should().ContainKey("state").WhoseValue.ToString().Should().NotBeEmpty();
        asked.Should().ContainKey("nonce").WhoseValue.ToString().Should().NotBeEmpty();
    }

    /// <summary>
    /// The form posts are what the protection is for. A page left open shows data; an upload left
    /// open runs code, so the same redirect answers a post, and nothing of what was posted lands.
    /// </summary>
    [Fact]
    public async Task Sends_an_anonymous_upload_to_the_provider_and_installs_nothing()
    {
        using var web = MemoriaWeb.SigningIn();
        using var upload = new MultipartFormDataContent
        {
            { new ByteArrayContent(Forms.Zip("Contoso.Orders.dll")), "files", "orders.zip" }
        };

        var response = await web.Client.PostAsync("/settings/upload", upload);

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.GetLeftPart(UriPartial.Path).Should().Be(MemoriaWeb.Provider.AuthorizeEndpoint);
        web.Installed.Should().BeEmpty();
    }

    /// <summary>
    /// Signed in, the pages answer, and the header says who the tool thinks is asking — the one
    /// place an operator on a shared machine finds out whose session they are in.
    /// </summary>
    [Fact]
    public async Task Shows_a_signed_in_operator_the_pages_and_their_name()
    {
        using var web = MemoriaWeb.SignedInAs("Ada Lovelace");

        var response = await web.Client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        Markup.Header(await response.Content.ReadAsStringAsync()).Should().Contain("Ada Lovelace");
    }

    [Fact]
    public async Task Answers_anyone_when_told_to_run_open()
    {
        using var web = MemoriaWeb.Open();

        var response = await web.Client.GetAsync("/streamed");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Running open is a choice somebody made, and the log is where they find out it is still in
    /// force. A warning rather than a line among the others, because an upload form anyone can
    /// reach is the thing this whole arrangement exists to prevent.
    /// </summary>
    [Fact]
    public async Task Warns_at_start_up_that_it_is_running_open()
    {
        using var web = MemoriaWeb.Open();

        await web.Client.GetAsync("/streamed");

        web.Logged.Should().Contain(entry =>
            entry.Level == LogLevel.Warning && entry.Message.Contains("Authentication:Disabled"));
    }

    [Fact]
    public async Task Says_at_start_up_which_provider_operators_sign_in_through()
    {
        using var web = MemoriaWeb.SigningIn();

        await web.Client.GetAsync("/streamed");

        web.Logged.Should().Contain(entry =>
            entry.Level == LogLevel.Information && entry.Message.Contains(MemoriaWeb.Provider.Authority));
    }

    [Fact]
    public void Refuses_to_start_when_told_neither_to_sign_in_nor_to_run_open()
    {
        using var web = MemoriaWeb.Unconfigured();

        var starting = () => web.Client;

        starting.Should().Throw<InvalidOperationException>()
            .Which.Message.Should()
            .Contain("Authentication:Oidc:Authority").And
            .Contain("Authentication:Oidc:ClientId").And
            .Contain("Authentication:Oidc:ClientSecret").And
            .Contain("Authentication:Disabled");
    }
}
