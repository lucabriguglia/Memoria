using Memoria.EventSourcing.Domain;

namespace Memoria.Examples.EventSourcing.Dcb.EntityFrameworkCore.Events;

[EventType("CourseDefined")]
public record CourseDefinedEvent(string CourseId, int Capacity) : IEvent;

[EventType("StudentRegistered")]
public record StudentRegisteredEvent(string StudentId, string Name) : IEvent;

[EventType("StudentSubscribed")]
public record StudentSubscribedEvent(string StudentId, string CourseId) : IEvent;

[EventType("StudentUnsubscribed")]
public record StudentUnsubscribedEvent(string StudentId, string CourseId) : IEvent;

[EventType("CourseCapacityChanged")]
public record CourseCapacityChangedEvent(string CourseId, int Capacity) : IEvent;
