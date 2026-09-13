using Microsoft.AspNetCore.Authorization;

namespace Memoria.Web.Security;

/// <summary>
/// What a policy asks for: one of the three roles, held outright or included in a higher one.
/// </summary>
/// <param name="Role">The role the policy is named after.</param>
/// <remarks>
/// A requirement of its own rather than the framework's claim check so the policy can say which
/// role it was, which is what the forbidden page tells the operator, and so that the order of the
/// three lives in one place: an Administrator is an Updater is a Reader.
/// </remarks>
public sealed record RoleRequirement(string Role) : IAuthorizationRequirement
{
    /// <summary>The three, lowest first; a role includes every one before it.</summary>
    private static readonly string[] Ordered = [Roles.Reader, Roles.Updater, Roles.Administrator];

    /// <summary>Whether holding <paramref name="held"/> satisfies a requirement for <see cref="Role"/>.</summary>
    public bool IsMetBy(string held) =>
        Array.IndexOf(Ordered, held) >= Array.IndexOf(Ordered, Role);

    /// <summary>Meets the requirement from the role claims the transformation added.</summary>
    public sealed class Handler : AuthorizationHandler<RoleRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, RoleRequirement requirement)
        {
            if (context.User.FindAll(Roles.ClaimType).Any(claim => requirement.IsMetBy(claim.Value)))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}
