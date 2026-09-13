using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Memoria.Web.Security;

/// <summary>
/// Signs operators in the way the settings ask, or leaves the tool open when they ask for that.
/// </summary>
public static class SignInRegistration
{
    /// <summary>
    /// Registers the sign-in the settings describe.
    /// </summary>
    /// <param name="services">The application's services.</param>
    /// <param name="settings">Which sign-in, read off configuration before anything listens.</param>
    /// <returns>The same services, so calls can be chained.</returns>
    /// <remarks>
    /// With a provider, every endpoint requires a signed-in operator unless it says otherwise: the
    /// requirement is the fallback policy, which applies to whatever declares no policy of its own.
    /// That is the whole of the protection, and it is why nothing is left to each endpoint
    /// remembering to ask — an endpoint added without a thought is a protected endpoint. The
    /// exceptions are declared where they are mapped, and a test pins the list.
    ///
    /// Open, no scheme is registered and no policy: the middleware is there, since the pipeline is
    /// the same in both modes, but it has nobody to ask and nothing to require, and the tool
    /// answers as it did before it had either.
    /// </remarks>
    public static IServiceCollection AddSignIn(this IServiceCollection services, AuthenticationSettings settings)
    {
        if (settings is not AuthenticationSettings.OpenIdConnect provider)
        {
            services.AddAuthentication();
            services.AddAuthorization();
            return services;
        }

        services.AddAuthorization(options =>
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        services.AddAuthentication(options =>
            {
                // The session is the cookie; the provider is asked only when there is none.
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie()
            .AddOpenIdConnect(options =>
            {
                options.Authority = provider.Authority;
                options.ClientId = provider.ClientId;
                options.ClientSecret = provider.ClientSecret;

                // The authorization code flow, with a proof key. Said explicitly because the
                // handler's own default is still the implicit flow, which hands the token to the
                // browser in the front channel — the one thing this tool must not do.
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.UsePkce = true;

                options.Scope.Clear();
                foreach (var scope in provider.Scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    options.Scope.Add(scope);
                }

                // The name and email are usually in the ID token, and read from UserInfo when the
                // provider left them out of it.
                options.GetClaimsFromUserInfoEndpoint = true;

                // Claims keep the names the provider gave them, rather than being renamed to the
                // SOAP-era URIs the handler maps them to by default. Whichever claim a provider puts
                // its roles in is then asked for by that name.
                options.MapInboundClaims = false;
                options.TokenValidationParameters.NameClaimType = "name";

                // The session lasts as long as the identity the provider issued, not a sliding
                // window of its own. Kept for the ID token too, which sign-out hands back to the
                // provider as the hint of which session to end.
                options.UseTokenLifetime = true;
                options.SaveTokens = true;
            });

        return services;
    }

    /// <summary>
    /// Says which of the two the tool is running with, once, when it starts.
    /// </summary>
    /// <param name="logger">The application's logger.</param>
    /// <param name="settings">Which sign-in was read.</param>
    /// <remarks>
    /// Open is a warning rather than a line among the others. It is a choice somebody wrote down,
    /// and the log is where whoever reads it later finds out it is still in force.
    /// </remarks>
    public static void LogSignIn(this ILogger logger, AuthenticationSettings settings)
    {
        if (settings is AuthenticationSettings.OpenIdConnect provider)
        {
            logger.LogInformation("Operators sign in through {Authority}.", provider.Authority);
            return;
        }

        logger.LogWarning(
            "Running open: nobody is signed in and every page, including the upload form, answers " +
            "anyone who can reach it, because {Setting} is true.", AuthenticationSettings.DisabledSetting);
    }
}
