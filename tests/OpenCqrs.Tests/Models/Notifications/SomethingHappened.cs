using Memoria.Notifications;

namespace OpenCqrs.Tests.Models.Notifications;

public record SomethingHappened(string Name) : INotification;
