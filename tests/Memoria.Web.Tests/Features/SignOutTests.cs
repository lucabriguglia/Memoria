using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// Leaving. A signed-in operator posts the sign-out form, the session cookie is gone, and the
/// provider is told to end its own session and send them to a page that says so — one they can
/// read without a session, since they no longer have one.
/// </summary>
public class SignOutTests
{
    [Fact]
    public async Task Ends_the_session_and_sends_the_operator_through_the_provider_to_the_signed_out_page()
    {
        using var web = MemoriaWeb.SignedInAs("Ada Lovelace");
        var client = web.Client;

        // The form's own token, read off the page the button is on.
        var page = await client.GetStringAsync("/");
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [Forms.AntiforgeryField] = Forms.AntiforgeryToken(page)
        });

        var response = await client.PostAsync("/logout", form);

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        var location = response.Headers.Location!;
        location.GetLeftPart(UriPartial.Path).Should().Be(MemoriaWeb.Provider.EndSessionEndpoint);

        // The provider is asked to come back to the tool's own sign-out callback, which is where
        // the promise to land on the signed-out page is kept — carried there in the state.
        var asked = QueryHelpers.ParseQuery(location.Query);
        asked["post_logout_redirect_uri"].ToString().Should().Be("http://localhost/signout-callback-oidc");

        response.Headers.GetValues("Set-Cookie").Should().Contain(cookie =>
            cookie.StartsWith(".AspNetCore.Cookies=", StringComparison.Ordinal) &&
            cookie.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase));

        var returned = await client.GetAsync($"/signout-callback-oidc?state={Uri.EscapeDataString(asked["state"]!)}");

        returned.StatusCode.Should().Be(HttpStatusCode.Found);
        returned.Headers.Location!.ToString().Should().Be("/signed-out");
    }

    [Fact]
    public async Task Shows_the_signed_out_page_to_anyone_with_a_way_back_in()
    {
        using var web = MemoriaWeb.SigningIn();

        var response = await web.Client.GetAsync("/signed-out");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().MatchRegex("""<a[^>]*href="/?"[^>]*>""");
    }

    private static string AntiforgeryToken(string page) =>
        Regex.Match(page, "__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
}
