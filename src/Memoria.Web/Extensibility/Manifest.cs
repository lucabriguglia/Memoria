using System.Text.Json;
using System.Text.RegularExpressions;

namespace Memoria.Web.Extensibility;

/// <summary>
/// What a zip must carry at its root, <c>memoria.json</c>: the services the archive brings.
/// </summary>
/// <param name="Services">The services declared, in the order they were written.</param>
/// <remarks>
/// A trust boundary: the file arrives inside an upload, so it is parsed as plain JSON and then held
/// to each rule in turn, and the first rule it breaks is the reason it is refused — a reason in
/// words, since it is read on the settings page by whoever uploaded the zip. Keys the manifest
/// carries that this version does not read are ignored, so a later version may add to the shape
/// without an older tool refusing what it wrote.
/// </remarks>
public sealed partial record Manifest(IReadOnlyList<Service> Services)
{
    /// <summary>The name the file is carried under, at the archive root.</summary>
    public const string FileName = "memoria.json";

    /// <summary>What a service name may be made of: it is the address the service is browsed under.</summary>
    [GeneratedRegex("^[A-Za-z0-9-]+$")]
    private static partial Regex ServiceName();

    /// <summary>
    /// Reads a manifest, refusing one that breaks a rule.
    /// </summary>
    /// <param name="json">The file's text.</param>
    /// <returns>The services declared.</returns>
    /// <exception cref="InvalidDataException">
    /// The text is not a JSON object, declares no services, or declares a service with no name, a
    /// name that is not letters, digits and hyphens, a name already declared in the same file, no
    /// assemblies, or no connection string.
    /// </exception>
    public static Manifest Parse(string json)
    {
        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"{FileName} could not be read: {exception.Message}");
        }

        using (document)
        {
            if (document.RootElement.ValueKind is not JsonValueKind.Object)
            {
                throw new InvalidDataException($"{FileName} could not be read: it is not a JSON object.");
            }

            if (!document.RootElement.TryGetProperty("services", out var declared) ||
                declared.ValueKind is not JsonValueKind.Array ||
                declared.GetArrayLength() == 0)
            {
                throw new InvalidDataException($"{FileName} declares no services.");
            }

            var services = new List<Service>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var element in declared.EnumerateArray())
            {
                var service = ReadService(element);

                if (!names.Add(service.Name))
                {
                    throw new InvalidDataException($"{FileName} declares the service '{service.Name}' twice.");
                }

                services.Add(service);
            }

            return new Manifest(services);
        }
    }

    private static Service ReadService(JsonElement element)
    {
        var name = Text(element, "name");
        if (name is null)
        {
            throw new InvalidDataException($"A service in {FileName} has no name.");
        }

        if (!ServiceName().IsMatch(name))
        {
            throw new InvalidDataException(
                $"The service name '{name}' is not letters, digits and hyphens, which is all a name may be made of.");
        }

        var assemblies = Texts(element, "assemblies");
        if (assemblies.Count == 0)
        {
            throw new InvalidDataException($"The service '{name}' names no assemblies.");
        }

        var connectionString = Text(element, "connectionString");
        if (connectionString is null)
        {
            throw new InvalidDataException($"The service '{name}' names no connection string.");
        }

        var roles = element.TryGetProperty("roles", out var declaredRoles) && declaredRoles.ValueKind is JsonValueKind.Object
            ? declaredRoles
            : default;

        return new Service(
            name,
            assemblies,
            connectionString,
            ReadRoles: roles.ValueKind is JsonValueKind.Object ? Texts(roles, "read") : [],
            UpdateRoles: roles.ValueKind is JsonValueKind.Object ? Texts(roles, "update") : []);
    }

    /// <summary>One string property, or null when absent, not a string, or blank.</summary>
    private static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) &&
        value.ValueKind is JsonValueKind.String &&
        value.GetString() is { } text &&
        !string.IsNullOrWhiteSpace(text)
            ? text
            : null;

    /// <summary>The non-blank strings of one array property, or nothing when absent or not an array.</summary>
    private static IReadOnlyList<string> Texts(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind is JsonValueKind.Array
            ? value.EnumerateArray()
                .Where(item => item.ValueKind is JsonValueKind.String)
                .Select(item => item.GetString()!)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToList()
            : [];
}

/// <summary>
/// One service an archive declares: a named set of domain assemblies read over one store, and who
/// may read and update it.
/// </summary>
/// <param name="Name">
/// The name it is known and browsed under — letters, digits and hyphens, unique across every
/// installed archive without regard to case.
/// </param>
/// <param name="Assemblies">
/// The assembly files its domain types are read from, by file name. Nothing else in the archive
/// is scanned; the rest is loaded as dependencies only.
/// </param>
/// <param name="ConnectionString">
/// The name of the connection string it is read over — an entry under <c>ConnectionStrings</c> in
/// the tool's configuration, never the string itself.
/// </param>
/// <param name="ReadRoles">
/// The claim values that let an operator read it, beside the global roles. Empty means only the
/// global roles reach it.
/// </param>
/// <param name="UpdateRoles">The claim values that let an operator update it as well. Update includes read.</param>
/// <remarks>
/// Not the framework's <c>IDomainService</c>, which is the object a store is read through: this
/// is the operator's word for the thing on the home page.
/// </remarks>
public sealed record Service(
    string Name,
    IReadOnlyList<string> Assemblies,
    string ConnectionString,
    IReadOnlyList<string> ReadRoles,
    IReadOnlyList<string> UpdateRoles);
