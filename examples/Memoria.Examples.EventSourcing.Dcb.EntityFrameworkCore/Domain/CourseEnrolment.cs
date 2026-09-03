using Memoria.EventSourcing.Dcb;
using Memoria.EventSourcing.Domain;
using Memoria.Examples.EventSourcing.Dcb.EntityFrameworkCore.Events;

namespace Memoria.Examples.EventSourcing.Dcb.EntityFrameworkCore.Domain;

/// <summary>
/// One student's standing on one course — the answer to "is alice on maths?".
/// </summary>
/// <remarks>
/// <para>
/// The counterpart to <see cref="SubscriptionDecision"/>, and the reason a boundary comes in two
/// shapes. That model asks a question about a course <em>and</em> a student, so its boundary is the
/// union <c>course:maths OR student:alice</c> and it folds every seat in the course and every course
/// the student has ever taken. This one asks a question about the pair, so its boundary is the
/// intersection <c>course:maths AND student:alice</c> and it folds only the events concerning both —
/// in a school of any size, a handful rather than thousands.
/// </para>
/// <para>
/// Notice what the narrower boundary removes from the fold. <see cref="SubscriptionDecision.Apply"/>
/// guards nearly every case on <c>== CourseId</c> or <c>== StudentId</c>, because its union brings in
/// other students' subscriptions to this course and this student's subscriptions to other courses,
/// and the model has to sort them out itself. Here the boundary has already done that: every event
/// that reaches <see cref="Apply{T}"/> concerns this student and this course, so there is nothing to
/// check.
/// </para>
/// <para>
/// It follows that an intersection is only as good as the tagging. A subscription is appended under
/// both tags, so it lands here; <c>CourseDefinedEvent</c> is appended under the course alone and
/// <c>StudentRegisteredEvent</c> under the student alone, so neither is inside this boundary however
/// much it might seem to concern the pair. That is the right answer for this question and the wrong
/// one for the subscription rule — which is exactly why that rule reads the union.
/// </para>
/// </remarks>
[ProjectionType("CourseEnrolment")]
public class CourseEnrolment : DcbProjection
{
    /// <summary>
    /// Whether the student is on the course right now.
    /// </summary>
    public bool IsEnrolled { get; private set; }

    /// <summary>
    /// How many times they have joined it, so a rejoin after leaving is visible rather than implied.
    /// </summary>
    public int TimesJoined { get; private set; }

    public override Type[]? EventTypeFilter { get; } =
    [
        typeof(StudentSubscribedEvent),
        typeof(StudentUnsubscribedEvent)
    ];

    protected override bool Apply<T>(T @event)
    {
        switch (@event)
        {
            // No `when subscribed.CourseId == CourseId` guard, unlike SubscriptionDecision. The
            // boundary is the guard: an event only reaches this fold if it carries both tags.
            case StudentSubscribedEvent:
                IsEnrolled = true;
                TimesJoined++;
                return true;

            case StudentUnsubscribedEvent:
                IsEnrolled = false;
                return true;

            default:
                return false;
        }
    }
}

/// <summary>
/// Identifies the enrolment, and carries the intersection boundary it is folded from.
/// </summary>
/// <remarks>
/// The boundary reads "events carrying <c>course:{courseId}</c> <em>and</em> <c>student:{studentId}</c>",
/// where <see cref="SubscriptionDecisionId.Boundary"/> reads "<em>or</em>".
/// </remarks>
public class CourseEnrolmentId(string courseId, string studentId) : IDcbProjectionId<CourseEnrolment>
{
    public string Id { get; } = $"{courseId}-{studentId}";

    public TagQuery Boundary { get; } =
        TagQuery.AllOf(new Tag("course", courseId), new Tag("student", studentId));
}
