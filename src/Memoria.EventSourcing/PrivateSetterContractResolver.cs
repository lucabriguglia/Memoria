using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace OpenCqrs.EventSourcing;

/// <summary>
/// Custom JSON contract resolver that enables JSON.NET to deserialize objects with private setters.
/// Extends <see cref="DefaultContractResolver"/> to allow setting properties that have private set methods,
/// which is useful for deserializing immutable objects or entities with controlled property access.
/// </summary>
public class PrivateSetterContractResolver : DefaultContractResolver
{
    /// <summary>
    /// Creates a <see cref="JsonProperty"/> for the given member and enables writing to properties
    /// that have private set methods.
    /// </summary>
    /// <param name="member">The member for which to create the JSON property. This can be a property or field.</param>
    /// <param name="memberSerialization">The member serialization mode that determines how the member should be serialized.</param>
    /// <returns>
    /// A <see cref="JsonProperty"/> configured to allow writing to properties with private setters.
    /// If the property already has a public setter, it remains unchanged. If it has a private setter,
    /// the <see cref="JsonProperty.Writable"/> property is set to true to enable deserialization.
    /// </returns>
    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
    {
        var jsonProperty = base.CreateProperty(member, memberSerialization);

        if (jsonProperty.Writable)
        {
            return jsonProperty;
        }

        if (member is PropertyInfo propertyInfo)
        {
            jsonProperty.Writable = propertyInfo.GetSetMethod(true) != null;
        }

        return jsonProperty;
    }
}
