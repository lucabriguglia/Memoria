using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Memoria.Web.Security;

// Aliased inside the namespace for the reason EndpointRegistration gives: under Memoria, a plain
// Results binds to the framework namespace of that name rather than to the class.
using Results = Microsoft.AspNetCore.Http.Results;

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
    public static IServiceCollection AddSignIn(
        this IServiceCollection services, AuthenticationSettings settings, AuthorizationSettings roles)
    {
        // What the layout asks to say who is signed in. Registered in both modes so the layout is
        // one layout: open, it is asked and answers nobody. The settings themselves are there too,
        // for the one thing the layout draws differently open — the operator's corner of the bar,
        // which has no operator to name.
        services.AddCascadingAuthenticationState();
        services.AddSingleton(settings);

        if (settings is not AuthenticationSettings.OpenIdConnect provider)
        {
            services.AddAuthentication();

            // The three policies exist so that the pages and posts naming them can be mapped, and
            // each is met by anyone: open, there is nobody to hold a role, and nothing is kept
            // from the nobody who is asking. The access decision the pages ask directly answers
            // the same way.
            services.AddSingleton(ServiceAccess.Open);
            services.AddAuthorization(options =>
            {
                foreach (var role in new[] { Roles.Reader, Roles.Updater, Roles.Administrator })
                {
                    options.AddPolicy(role, policy => policy.RequireAssertion(_ => true));
                }
            });

            return services;
        }

        services.AddSingleton(roles);
        services.AddSingleton(new ServiceAccess(roles));
        services.AddTransient<IClaimsTransformation, RoleClaims>();
        services.AddSingleton<IAuthorizationHandler, RoleRequirement.Handler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, ForbiddenRedirect>();

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

            // One policy per role, named after it, so a page or a post asks for the role by name
            // and the refusal can say which it was.
            foreach (var role in new[] { Roles.Reader, Roles.Updater, Roles.Administrator })
            {
                options.AddPolicy(role, policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(new RoleRequirement(role)));
            }
        });

        services.AddAuthentication(options =>
            {
                // The session is the cookie; the provider is asked only when there is none.
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                // Where a signed-in operator lacking a role is sent. The redirect itself is built
                // by ForbiddenRedirect, which adds the role; this is the framework's own fallback
                // for a refusal that carries none.
                options.AccessDeniedPath = ForbiddenRedirect.Path;

                // The session is the identity the provider issued, and ends when it does. The
                // framework's default reissues a cookie past its half-life with a fresh expiry —
                // right for a site where the cookie is the only identity, and here a way for a
                // session to outlive its token one page at a time. Off, the cookie's expiry is
                // the token's, which UseTokenLifetime below sets it to.
                options.SlidingExpiration = false;
            })
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
    /// Maps the sign-out the layout's button posts to, when there is a sign-in to end.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <param name="settings">Which sign-in was read.</param>
    /// <returns>The same application, so calls can be chained.</returns>
    /// <remarks>
    /// Both sessions are ended: the cookie here, and the provider's own, which is asked to send
    /// the operator back to a page that says so. That page is the one address an anonymous
    /// caller may read, since whoever lands on it has just stopped being anybody. Open, nothing is
    /// mapped — there is no session to end and no button posting here.
    /// </remarks>
    public static WebApplication MapSignOut(this WebApplication app, AuthenticationSettings settings)
    {
        if (settings is not AuthenticationSettings.OpenIdConnect)
        {
            return app;
        }

        // Antiforgery in so many words, since there is no form to bind that would mark it: the
        // button is a form post like the others and is checked like them.
        app.MapPost("/logout", () => Results.SignOut(
                new AuthenticationProperties { RedirectUri = SignedOutPath },
                [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]))
            .WithMetadata(new RequiresAntiforgery());

        return app;
    }

    /// <summary>
    /// The mark binding a form puts on an endpoint, put on one by hand. The framework's own type
    /// for it is internal; the interface the middleware reads is not.
    /// </summary>
    private sealed class RequiresAntiforgery : IAntiforgeryMetadata
    {
        public bool RequiresValidation => true;
    }

    /// <summary>Where the provider sends an operator who has signed out.</summary>
    public const string SignedOutPath = "/signed-out";

    /// <summary>
    /// Says which of the two the tool is running with, once, when it starts.
    /// </summary>
    /// <param name="logger">The application's logger.</param>
    /// <param name="settings">Which sign-in was read.</param>
    /// <remarks>
    /// Open is a warning rather than a line among the others. It is a choice somebody wrote down,
    /// and the log is where whoever reads it later finds out it is still in force.
    /// </remarks>
    public static void LogSignIn(this ILogger logger, AuthenticationSettings settings, AuthorizationSettings roles)
    {
        if (settings is not AuthenticationSettings.OpenIdConnect provider)
        {
            logger.LogWarning(
                "Running open: nobody is signed in and every page, including the upload form, answers " +
                "anyone who can reach it, because {Setting} is true.", AuthenticationSettings.DisabledSetting);
            return;
        }

        logger.LogInformation("Operators sign in through {Authority}.", provider.Authority);

        if (roles.MapsAnyone)
        {
            logger.LogInformation(
                "Roles are read off the {Claim} claim: Administrator for {Administrators}, Updater for {Updaters}, Reader for {Readers}.",
                roles.RoleClaimType,
                string.Join(", ", roles.Administrators),
                string.Join(", ", roles.Updaters),
                string.Join(", ", roles.Readers));
            return;
        }

        // Said at start-up rather than discovered at the upload form. Silence is the safe reading,
        // but an administrator who forgot the mapping should hear about it here first.
        logger.LogInformation(
            "No roles are mapped: a signed-in operator sees only the services whose manifest names a " +
            "claim value they hold, and nobody can use Settings. Set {Administrator}, {Updater} and " +
            "{Reader} to the claim values that grant each role for every service.",
            AuthorizationSettings.AdministratorSetting,
            AuthorizationSettings.UpdaterSetting,
            AuthorizationSettings.ReaderSetting);
    }
}
