using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// A session is the cookie, and the cookie lives exactly as long as the identity the provider
/// issued: not renewed on the quiet because more than half of it has gone by, and not answered
/// once it has ended. These send a cookie the application's own format made, so what they exercise
/// is the real cookie path rather than the scheme the other tests stand it in with.
/// </summary>
public class SessionTests
{
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    [Fact]
    public async Task Answers_a_live_session_with_the_page_and_the_name_it_carries()
    {
        using var web = MemoriaWeb.SigningIn();
        var now = DateTimeOffset.UtcNow;

        var response = await Send(web, "/", web.SessionCookie("Ada Lovelace", now, now + Hour));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        Markup.Header(await response.Content.ReadAsStringAsync()).Should().Contain("Ada Lovelace");
    }

    /// <summary>
    /// Ended a minute ago, the cookie is nobody, and nobody is sent to sign in — to the provider,
    /// not to an error and not to the page.
    /// </summary>
    [Fact]
    public async Task Sends_an_expired_session_to_the_provider_to_sign_in()
    {
        using var web = MemoriaWeb.SigningIn();
        var now = DateTimeOffset.UtcNow;

        var response = await Send(web, "/streamed",
            web.SessionCookie("Ada Lovelace", now - Hour, now - TimeSpan.FromMinutes(1)));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.GetLeftPart(UriPartial.Path).Should().Be(MemoriaWeb.Provider.AuthorizeEndpoint);
    }

    /// <summary>
    /// The framework's default is to reissue a cookie past its half-life with a fresh expiry,
    /// which would carry a session past the identity it was issued for, one page at a time. Off,
    /// the response carries no new cookie: the one the operator has is the one they keep.
    /// </summary>
    [Fact]
    public async Task Does_not_renew_a_session_past_its_half_life()
    {
        using var web = MemoriaWeb.SigningIn();
        var now = DateTimeOffset.UtcNow;

        var response = await Send(web, "/",
            web.SessionCookie("Ada Lovelace", now - TimeSpan.FromMinutes(40), now + TimeSpan.FromMinutes(20)));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        SessionCookiesSet(response).Should().BeEmpty();
    }

    /// <summary>What the cookie is issued as, read off the options: configuration evidence.</summary>
    [Fact]
    public void Issues_a_cookie_the_browser_keeps_to_itself_and_does_not_slide()
    {
        using var web = MemoriaWeb.SigningIn();
        _ = web.Client;

        var options = web.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);

        options.SlidingExpiration.Should().BeFalse();
        options.Cookie.HttpOnly.Should().BeTrue();
        options.Cookie.SameSite.Should().Be(SameSiteMode.Lax);
        options.Cookie.SecurePolicy.Should().Be(CookieSecurePolicy.SameAsRequest);
    }

    private static async Task<HttpResponseMessage> Send(MemoriaWeb web, string path, string cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", cookie);

        return await web.BareClient.SendAsync(request);
    }

    private static string[] SessionCookiesSet(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var set)
            ? set.Where(value => value.StartsWith(".AspNetCore.Cookies=", StringComparison.Ordinal)).ToArray()
            : [];
}
