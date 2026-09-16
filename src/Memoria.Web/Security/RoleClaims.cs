using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace Memoria.Web.Security;

/// <summary>
/// Turns what the provider said about an operator into the global roles the tool understands.
/// </summary>
/// <remarks>
/// Runs on every authenticated request, whichever scheme authenticated it, and reads the claim
/// the settings name for the values the settings map. What it adds is a claim of the tool's own
/// type, so a group the provider happens to call Administrator grants nothing until configuration
/// says it does. Nothing is added for a value nothing maps — not Reader either: since a service's
/// manifest may name its own readers, silence is nobody rather than everybody. Idempotent, as a
/// transformation has to be: asked twice about the same principal it adds nothing the second
/// time, which the marker claim below is for.
/// </remarks>
public sealed class RoleClaims(AuthorizationSettings settings) : IClaimsTransformation
{
    /// <summary>
    /// The claim that says the principal has been looked at, whether or not a role was added —
    /// so a principal mapped to nothing is not mapped again on every request.
    /// </summary>
    public const string MappedClaimType = "memoria-web:mapped";

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity { IsAuthenticated: true } identity ||
            identity.HasClaim(claim => claim.Type == MappedClaimType))
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

        if (settings.Readers.Any(sent.Contains))
        {
            identity.AddClaim(new Claim(Roles.ClaimType, Roles.Reader));
        }

        identity.AddClaim(new Claim(MappedClaimType, "true"));

        return Task.FromResult(principal);
    }
}
