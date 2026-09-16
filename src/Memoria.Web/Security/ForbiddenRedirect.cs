using Memoria.Web.Extensibility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.WebUtilities;

namespace Memoria.Web.Security;

/// <summary>
/// Sends a signed-in operator who lacks the role an address needs to the page that says so,
/// carrying which role, which service the address was under, and which address.
/// </summary>
/// <remarks>
/// With a cookie session a refusal is a redirect, not a bare 403, and the framework's own would
/// go to the cookie's access-denied path with nothing but where the operator was going. This adds
/// the role the policy was named after and the service the address was under — the two things
/// the page has to say, since either the configuration or that service's manifest is where the
/// role could be granted. Anyone not signed in at all is left to the framework, which sends them
/// to the provider.
/// </remarks>
public sealed class ForbiddenRedirect(DomainTypeRegistry types) : IAuthorizationMiddlewareResultHandler
{
    /// <summary>The page a refused operator is sent to.</summary>
    public const string Path = "/forbidden";

    private readonly AuthorizationMiddlewareResultHandler _framework = new();

    public Task HandleAsync(
        RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        var role = policy.Requirements.OfType<RoleRequirement>().FirstOrDefault()?.Role;

        if (!authorizeResult.Forbidden || context.User.Identity?.IsAuthenticated != true || role is null)
        {
            return _framework.HandleAsync(next, context, policy, authorizeResult);
        }

        context.Response.Redirect(Address(role, RoleRequirement.ServiceOf(context, types), Asked(context)));

        return Task.CompletedTask;
    }

    /// <summary>The address a request asked for, as the forbidden page shows it back.</summary>
    public static string Asked(HttpContext context) =>
        context.Request.PathBase + context.Request.Path + context.Request.QueryString;

    /// <summary>
    /// The forbidden page's address, carrying the role the address needed, the service it was
    /// under when it was under one, and the address itself.
    /// </summary>
    public static string Address(string role, Service? service, string returnUrl) =>
        QueryHelpers.AddQueryString(Path, new Dictionary<string, string?>
        {
            ["role"] = role,
            ["service"] = service?.Name,
            ["returnUrl"] = returnUrl
        });
}
