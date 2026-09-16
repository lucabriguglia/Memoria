using System.Reflection;
using Memoria.EventSourcing.Domain;

namespace Memoria.Web.Extensibility;

/// <summary>
/// Holds the domain types the application knows about, and rebuilds them from scratch on demand.
/// </summary>
/// <param name="store">Where the uploaded assemblies live.</param>
/// <param name="hosts">
/// The application's own assemblies, scanned alongside the uploads so anything they declare
/// survives a reload — the one the application was built from, in production. Empty when there is
/// nothing of its own to contribute.
/// </param>
/// <remarks>
/// A reload replaces what every request on this server resolves, for everyone, at once: the
/// catalogue is published whole, so a request reading it concurrently sees either the previous one
/// or the new one, never half of each. That is what makes an upload take effect without a restart,
/// and it is also why one bad upload is everybody's problem. Pages already rendered are not told;
/// a browser refresh picks the new set up.
/// <para>
/// The bindings a store is read through are built per service, from that service's assemblies
/// alone, and handed to the service's stores; the framework's process-wide maps are left as they
/// are, so two services may bind one key to two types.
/// </para>
/// </remarks>
public sealed class DomainTypeRegistry(ExtensionStore store, params Assembly[] hosts)
{
    private readonly Lock _gate = new();

    /// <summary>Gets the types found by the last reload.</summary>
    public DomainTypeCatalogue Current { get; private set; } = DomainTypeCatalogue.Empty;

    /// <summary>
    /// Reads every uploaded assembly again and rebuilds the catalogue and every service's bindings
    /// from nothing.
    /// </summary>
    /// <remarks>
    /// From nothing rather than merged into what is already there, so a type that has been removed
    /// or renamed stops being offered.
    /// </remarks>
    public void Reload()
    {
        lock (_gate)
        {
            // What was worked out about the old types goes with them.
            IdentifierShape.Forget();
            IdShape.Forget();
            DomainTypeDescriber.Forget();

            var loaded = ExtensionLoader.Load(store);

            // Everything in the library is loaded, so a dependency resolves; only what a service
            // names is scanned, so the manifest is what says whose types are whose. A file nothing
            // names — a dependency, or a stray no installed archive accounts for — registers
            // nothing even when it carries attributed types of its own.
            var services = store.InstalledArchives()
                .SelectMany(archive => archive.Services)
                .ToList();

            var named = services
                .SelectMany(service => service.Assemblies)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var uploaded = loaded.Assemblies
                .Where(assembly => named.Contains(assembly.FileName))
                .Select(assembly => assembly.Assembly);
            var assemblies = new List<Assembly>([.. hosts, .. uploaded]);

            // The hosts under the file name a manifest would name each by, so a service may claim
            // what one declares the way it claims an upload; the name rather than the location,
            // since an assembly emitted at run time has none.
            var hostFiles = hosts
                .Select(host => new LoadedAssembly($"{host.GetName().Name}.dll", host))
                .ToList();

            var scanned = DomainTypeScanner.Scan(assemblies);
            var errors = new List<string>([.. loaded.Errors, .. scanned.Errors]);

            var catalogue = scanned with
            {
                Assemblies = loaded.Assemblies,
                Hosts = hostFiles,
                Services = services
            };

            // One set per service, from that service's own view of the catalogue: a key two of
            // its own types claim is reported, and the first keeps it; a key two services claim
            // is no clash at all, since each reads its own stores through its own set.
            var bindings = new Dictionary<string, TypeBindingSet>(StringComparer.OrdinalIgnoreCase);

            foreach (var service in services)
            {
                var own = catalogue.For(service);
                var what = $"{service.Name}: ";

                bindings[service.Slug] = new TypeBindingSet
                {
                    EventTypeBindings = Bind(own.Events, KeyOf<EventType>, what + "event", errors),
                    AggregateTypeBindings = Bind(own.StreamedAggregates, KeyOf<AggregateType>, what + "aggregate", errors),
                    ProjectionTypeBindings = Bind(own.StreamedProjections, KeyOf<ProjectionType>, what + "projection", errors),
                    DcbAggregateTypeBindings = Bind(own.DcbAggregates, KeyOf<AggregateType>, what + "DCB aggregate", errors),
                    DcbProjectionTypeBindings = Bind(own.DcbProjections, KeyOf<ProjectionType>, what + "DCB projection", errors)
                };
            }

            Current = catalogue with
            {
                Bindings = bindings,
                Errors = errors,
                ReloadedUtc = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Builds one binding map. A name and version claimed by two types is reported and the first
    /// keeps the name, rather than the reload failing and leaving nothing bound at all.
    /// </summary>
    private static Dictionary<string, Type> Bind(
        IEnumerable<Type> types, Func<Type, string?> keyOf, string what, List<string> errors)
    {
        var bindings = new Dictionary<string, Type>();

        foreach (var type in types)
        {
            // No attribute means the type is not meant to be bound; only an attributed type is.
            var key = keyOf(type);
            if (key is null)
            {
                continue;
            }

            if (bindings.TryGetValue(key, out var claimed))
            {
                errors.Add($"Two types claim the {what} name {key}: " +
                           $"{claimed.FullName} and {type.FullName}. {claimed.Name} keeps it.");
                continue;
            }

            bindings.Add(key, type);
        }

        return bindings;
    }

    private static string? KeyOf<TAttribute>(Type type) where TAttribute : Attribute =>
        type.GetCustomAttribute<TAttribute>() switch
        {
            EventType attribute => TypeBindings.GetTypeBindingKey(attribute.Name, attribute.Version),
            AggregateType attribute => TypeBindings.GetTypeBindingKey(attribute.Name, attribute.Version),
            ProjectionType attribute => TypeBindings.GetTypeBindingKey(attribute.Name, attribute.Version),
            _ => null
        };
}
