using Memoria.EventSourcing;
using Memoria.EventSourcing.Domain;
using Newtonsoft.Json;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;

internal static class EventSerialization
{
    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        ContractResolver = new PrivateSetterContractResolver()
    };

    public static string SerializeData(IEvent payload) => JsonConvert.SerializeObject(payload, JsonSettings);

    public static IEvent DeserializeData(string eventTypeKey, string data)
    {
        if (!TypeBindings.EventTypeBindings.TryGetValue(eventTypeKey, out var clrType))
        {
            throw new InvalidOperationException($"Event type '{eventTypeKey}' not found in TypeBindings.");
        }
        return (IEvent)JsonConvert.DeserializeObject(data, clrType, JsonSettings)!;
    }

    public static string GetEventTypeKey(IEvent payload)
    {
        var clrType = payload.GetType();
        var attribute = (EventType?)Attribute.GetCustomAttribute(clrType, typeof(EventType));
        if (attribute is null)
        {
            throw new InvalidOperationException($"Type '{clrType.FullName}' is missing the [EventType] attribute.");
        }
        return TypeBindings.GetTypeBindingKey(attribute.Name, attribute.Version);
    }
}
