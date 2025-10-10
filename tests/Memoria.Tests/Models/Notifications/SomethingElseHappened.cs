using Memoria.Notifications;

namespace Memoria.Tests.Models.Notifications;

public record SomethingElseHappened(string Name) : INotification;
