using Memoria.Web.Extensibility;
using Microsoft.AspNetCore.Authorization;

namespace Memoria.Web.Security;

/// <summary>
/// What a policy asks for: one of the three roles, held globally, or — for Reader and Updater
/// under a service — granted by that service's manifest.
/// </summary>
/// <param name="Role">The role the policy is named after.</param>
/// <remarks>
/// A requirement of its own rather than the framework's claim check so the policy can say which
/// role it was, which is what the forbidden page tells the operator. The decision itself is
/// <see cref="ServiceAccess"/>'s; what this adds is finding the service the request is under. An
/// endpoint's authorization carries the request, whose route names the service; a page's
/// <c>AuthorizeView</c> hands the service itself as its resource.
/// </remarks>
public sealed record RoleRequirement(string Role) : IAuthorizationRequirement
{
    /// <summary>Whether holding <paramref name="held"/> satisfies a requirement for <see cref="Role"/>.</summary>
    public bool IsMetBy(string held) => Roles.Includes(held, Role);

    /// <summary>Meets the requirement through the access decision, for the service in hand.</summary>
    public sealed class Handler(ServiceAccess access, DomainTypeRegistry types) : AuthorizationHandler<RoleRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, RoleRequirement requirement)
        {
            var service = context.Resource switch
            {
                Service named => named,
                HttpContext request => ServiceOf(request, types),
                _ => null
            };

            if (access.Grants(context.User, requirement.Role, service))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>The service a request's route puts it under, or null outside every service.</summary>
    public static Service? ServiceOf(HttpContext request, DomainTypeRegistry types) =>
        request.GetRouteValue("service") is string slug ? types.Current.ServiceAt(slug) : null;
}
