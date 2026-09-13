using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.WebUtilities;

namespace Memoria.Web.Security;

/// <summary>
/// Sends a signed-in operator who lacks the role an address needs to the page that says so,
/// carrying which role and which address.
/// </summary>
/// <remarks>
/// With a cookie session a refusal is a redirect, not a bare 403, and the framework's own would
/// go to the cookie's access-denied path with nothing but where the operator was going. This adds
/// the role the policy was named after, which is the one thing the page has to say. Anyone not
/// signed in at all is left to the framework, which sends them to the provider.
/// </remarks>
public sealed class ForbiddenRedirect : IAuthorizationMiddlewareResultHandler
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

        var returnUrl = context.Request.PathBase + context.Request.Path + context.Request.QueryString;

        context.Response.Redirect(QueryHelpers.AddQueryString(Path, new Dictionary<string, string?>
        {
            ["role"] = role,
            ["returnUrl"] = returnUrl
        }));

        return Task.CompletedTask;
    }
}
