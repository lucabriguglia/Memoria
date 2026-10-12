using Xunit;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests.Fixtures;

/// <summary>
/// A <see cref="FactAttribute"/> that skips instead of failing when there is neither a Docker endpoint
/// to run containers against nor an existing server configured, so the suite stays runnable on
/// machines without Docker.
/// </summary>
/// <remarks>
/// This covers Docker being absent. An endpoint that exists but cannot start the engine is left to
/// fail the test — that is a real problem, not a reason to quietly skip. So is a run with only one
/// engine's server configured: the other engine's tests fail, naming the variable to set.
/// </remarks>
public sealed class RequiresDockerFactAttribute : FactAttribute
{
    public RequiresDockerFactAttribute()
    {
        if (TestServerAvailability.Current is { } reason)
        {
            Skip = reason;
        }
    }
}

internal static class TestServerAvailability
{
    private const string UnixDockerSocket = "/var/run/docker.sock";

    // Docker Desktop exposes "docker_engine"; the WSL2 backend also exposes
    // "dockerDesktopLinuxEngine", and a context may point at either.
    private static readonly string[] WindowsDockerPipes =
    [
        @"\\.\pipe\docker_engine",
        @"\\.\pipe\dockerDesktopLinuxEngine"
    ];

    private static readonly string[] ServerVariables = [PostgreSqlFixture.Variable, SqlServerFixture.Variable];

    private static readonly string OrSetAServer =
        $" Or set {PostgreSqlFixture.Variable} and {SqlServerFixture.Variable} to the connection strings of existing servers.";

    private static readonly Lazy<string?> Probe = new(
        () => UnavailableReason(Environment.GetEnvironmentVariable, File.Exists, OperatingSystem.IsWindows()),
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Null when the suite can run here; otherwise why it is skipped.</summary>
    public static string? Current => Probe.Value;

    /// <summary>Null when the suite can run; otherwise why it is skipped.</summary>
    /// <param name="environment">Reads an environment variable.</param>
    /// <param name="pathExists">Whether a file or named pipe exists.</param>
    /// <param name="isWindows">Whether to look for Docker's named pipes rather than its socket.</param>
    public static string? UnavailableReason(Func<string, string?> environment, Func<string, bool> pathExists,
        bool isWindows)
    {
        // A configured server runs its engine's tests without Docker. With only one configured, the
        // other engine's fixture cannot start and its tests fail naming the variable to set; that is
        // a real gap in the run, not a reason to skip quietly.
        if (ServerVariables.Any(variable => !string.IsNullOrWhiteSpace(environment(variable))))
        {
            return null;
        }

        // An explicit endpoint is taken at its word; if it is wrong the container start will say so.
        if (!string.IsNullOrWhiteSpace(environment("DOCKER_HOST")))
        {
            return null;
        }

        try
        {
            if (isWindows)
            {
                return WindowsDockerPipes.Any(pathExists)
                    ? null
                    : "Docker is not available (no Docker named pipe found and DOCKER_HOST is unset). Is Docker Desktop running?" +
                      OrSetAServer;
            }

            return pathExists(UnixDockerSocket)
                ? null
                : $"Docker is not available (no socket at '{UnixDockerSocket}' and DOCKER_HOST is unset)." + OrSetAServer;
        }
        catch (Exception exception)
        {
            return $"Docker availability could not be determined ({exception.GetType().Name}: {exception.Message}).";
        }
    }
}
