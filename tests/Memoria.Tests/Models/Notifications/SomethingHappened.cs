using Memoria.Notifications;

namespace Memoria.Tests.Models.Notifications;

public record SomethingHappened(string Name) : INotification;
