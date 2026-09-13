using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace Memoria.Web.Security;

/// <summary>
/// Turns what the provider said about an operator into the roles the tool understands.
/// </summary>
/// <remarks>
/// Runs on every authenticated request, whichever scheme authenticated it, and reads the claim
/// the settings name for the values the settings map. What it adds is a claim of the tool's own
/// type, so a group the provider happens to call Administrator grants nothing until configuration
/// says it does. Idempotent, as a transformation has to be: asked twice about the same principal
/// it adds nothing the second time.
/// </remarks>
public sealed class RoleClaims(AuthorizationSettings settings) : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity { IsAuthenticated: true } identity ||
            identity.HasClaim(claim => claim.Type == Roles.ClaimType))
        {
            return Task.FromResult(principal);
        }

        var sent = principal.FindAll(settings.RoleClaimType).Select(claim => claim.Value).ToHashSet(StringComparer.Ordinal);

        if (settings.Administrators.Any(sent.Contains))
        {
            identity.AddClaim(new Claim(Roles.ClaimType, Roles.Administrator));
        }

        if (settings.Updaters.Any(sent.Contains))
        {
            identity.AddClaim(new Claim(Roles.ClaimType, Roles.Updater));
        }

        // Every signed-in operator, so that a principal carrying no role claim at all cannot be
        // mistaken for one that was never looked at.
        identity.AddClaim(new Claim(Roles.ClaimType, Roles.Reader));

        return Task.FromResult(principal);
    }
}
