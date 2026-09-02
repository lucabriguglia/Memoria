using Memoria.EventSourcing.Dcb;
using Memoria.EventSourcing.Domain;
using Memoria.Examples.EventSourcing.Dcb.EntityFrameworkCore.Events;

namespace Memoria.Examples.EventSourcing.Dcb.EntityFrameworkCore.Domain;

/// <summary>
/// What one student is enrolled in: a read model, built by folding their events and stored as a
/// snapshot so the next read does not start from the beginning of the log.
/// </summary>
/// <remarks>
/// <para>
/// A projection differs from an aggregate in one way only — it never produces events, so it has no
/// <c>Add</c> and no uncommitted events. Everything else is the same fold. It carries what a screen
/// wants rather than what a rule needs, which is why it keeps a student name no
/// decision in this example ever reads — a subscription only asks whether the student exists.
/// </para>
/// <para>
/// Its boundary is <c>student:{id}</c>, which no write model here uses: <see cref="Course"/> reads
/// one course, <see cref="SubscriptionDecision"/> reads a course and a student together. A read
/// model is free to draw its boundary wherever the question is, because it decides nothing and so
/// contends with nobody.
/// </para>
/// </remarks>
[ProjectionType("StudentTranscript")]
public class StudentTranscript : DcbProjection
{
    public string Name { get; private set; } = "unknown";

    public bool Registered { get; private set; }

    /// <summary>
    /// The courses the student is on now, in the order they joined them.
    /// </summary>
    /// <remarks>
    /// A private setter, not a computed property over a backing field. A projection is stored
    /// and read back, so whatever holds its state has to survive the round trip: the serializer
    /// writes every public getter but can only restore what it can set. The decision models in
    /// this example never notice, because they are folded fresh every time and never stored.
    /// </remarks>
    public IReadOnlyList<string> Courses { get; private set; } = [];

    /// <summary>
    /// Every event inside the boundary is this student's, because the boundary is one student tag.
    /// Subscriptions are tagged with the course as well, but the query never asks for that tag.
    /// </summary>
    public override Type[]? EventTypeFilter { get; } =
    [
        typeof(StudentRegisteredEvent),
        typeof(StudentSubscribedEvent),
        typeof(StudentUnsubscribedEvent)
    ];

    protected override bool Apply<T>(T @event)
    {
        switch (@event)
        {
            case StudentRegisteredEvent registered:
                Registered = true;
                Name = registered.Name;
                return true;

            case StudentSubscribedEvent subscribed:
                Courses = [..Courses, subscribed.CourseId];
                return true;

            case StudentUnsubscribedEvent unsubscribed:
                Courses = [..Courses.Where(course => course != unsubscribed.CourseId)];
                return true;

            default:
                return false;
        }
    }
}

/// <summary>
/// Identifies the transcript, and therefore its snapshot, and carries the boundary it is folded from.
/// </summary>
public class StudentTranscriptId(string studentId) : IDcbProjectionId<StudentTranscript>
{
    public string Id { get; } = studentId;

    public TagQuery Boundary { get; } = TagQuery.AnyOf(new Tag("student", studentId));
}
