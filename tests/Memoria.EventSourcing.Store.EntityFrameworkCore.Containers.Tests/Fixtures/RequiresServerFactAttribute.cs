using Xunit;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests.Fixtures;

/// <summary>
/// A <see cref="FactAttribute"/> for tests about an existing server: it runs only when the named
/// variable holds a connection string, and skips otherwise.
/// </summary>
public sealed class RequiresServerFactAttribute : FactAttribute
{
    public RequiresServerFactAttribute(string variable)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(variable)))
        {
            Skip = $"{variable} is not set, so there is no existing server to test against.";
        }
    }
}
