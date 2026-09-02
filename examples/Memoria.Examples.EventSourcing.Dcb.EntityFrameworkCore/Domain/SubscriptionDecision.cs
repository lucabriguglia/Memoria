using Memoria.EventSourcing.Dcb;
using Memoria.EventSourcing.Domain;
using Memoria.Examples.EventSourcing.Dcb.EntityFrameworkCore.Events;

namespace Memoria.Examples.EventSourcing.Dcb.EntityFrameworkCore.Domain;

/// <summary>
/// Everything one subscription decision needs to know, folded from the events of one course and one
/// student together.
/// </summary>
/// <remarks>
/// <para>
/// This is the shape a stream cannot hold. The rule is:
/// </para>
/// <list type="bullet">
/// <item><description>the course exists and is not full — a fact about the <em>course</em>;</description></item>
/// <item><description>the student is registered and not already on it — a fact about <em>both</em>;</description></item>
/// <item><description>the student is on fewer than ten courses — a fact about the <em>student</em>.</description></item>
/// </list>
/// <para>
/// A stream per course cannot see the student's other subscriptions; a stream per student cannot see
/// how full the course is. Putting both in one stream serialises every subscription in the school.
/// Here the boundary is the query <c>course:c1 OR student:s7</c>, so two subscriptions contend only
/// when they share a course or a student.
/// </para>
/// </remarks>
[AggregateType("SubscriptionDecision")]
public class SubscriptionDecision : DcbAggregateRoot
{
    private readonly HashSet<string> _coursesTheStudentIsOn = [];

    public const int MaximumCoursesPerStudent = 10;

    public string CourseId { get; private set; } = null!;

    public string StudentId { get; private set; } = null!;

    public bool CourseExists { get; private set; }

    public bool StudentExists { get; private set; }

    public int Capacity { get; private set; }

    public int SeatsTaken { get; private set; }

    public int CoursesTheStudentIsOn => _coursesTheStudentIsOn.Count;

    public bool AlreadySubscribed => _coursesTheStudentIsOn.Contains(CourseId);

    public bool CourseIsFull => SeatsTaken >= Capacity;

    public override Type[]? EventTypeFilter { get; } =
    [
        typeof(CourseDefinedEvent),
        typeof(CourseCapacityChangedEvent),
        typeof(StudentRegisteredEvent),
        typeof(StudentSubscribedEvent),
        typeof(StudentUnsubscribedEvent)
    ];

    /// <summary>
    /// Names the course and student this decision is about, so the fold can tell "this course" from
    /// the student's other courses.
    /// </summary>
    public SubscriptionDecision About(string courseId, string studentId)
    {
        CourseId = courseId;
        StudentId = studentId;
        return this;
    }

    /// <summary>
    /// Subscribes the student, or explains why it cannot.
    /// </summary>
    public string? Subscribe()
    {
        if (!CourseExists) return $"Course '{CourseId}' does not exist.";
        if (!StudentExists) return $"Student '{StudentId}' is not registered.";
        if (AlreadySubscribed) return $"Student '{StudentId}' is already on course '{CourseId}'.";
        if (CourseIsFull) return $"Course '{CourseId}' is full ({SeatsTaken}/{Capacity}).";

        if (CoursesTheStudentIsOn >= MaximumCoursesPerStudent)
        {
            return $"Student '{StudentId}' is already on {CoursesTheStudentIsOn} courses.";
        }

        // Tagged with both, so it moves either boundary: a later decision about this course sees it,
        // and so does a later decision about this student.
        Add(new StudentSubscribedEvent(StudentId, CourseId),
            new Tag("course", CourseId), new Tag("student", StudentId));

        return null;
    }

    protected override bool Apply<T>(T @event)
    {
        switch (@event)
        {
            case CourseDefinedEvent defined when defined.CourseId == CourseId:
                CourseExists = true;
                Capacity = defined.Capacity;
                return true;

            // A capacity change is as much a fact about the course as its definition. Adding an
            // event type is never only about the model that emits it: every model whose decision
            // depends on it has to filter and apply it too, or it silently decides on stale facts.
            case CourseCapacityChangedEvent changed when changed.CourseId == CourseId:
                Capacity = changed.Capacity;
                return true;

            case StudentRegisteredEvent registered when registered.StudentId == StudentId:
                StudentExists = true;
                return true;

            case StudentSubscribedEvent subscribed:
                // One event, two meanings. Seen through the course tag it fills a seat; seen through
                // the student tag it uses up one of their ten. The same event does both.
                if (subscribed.CourseId == CourseId) SeatsTaken++;
                if (subscribed.StudentId == StudentId) _coursesTheStudentIsOn.Add(subscribed.CourseId);
                return true;

            case StudentUnsubscribedEvent unsubscribed:
                if (unsubscribed.CourseId == CourseId) SeatsTaken--;
                if (unsubscribed.StudentId == StudentId) _coursesTheStudentIsOn.Remove(unsubscribed.CourseId);
                return true;

            default:
                return false;
        }
    }
}

/// <summary>
/// Identifies the decision, and therefore its snapshot. It does not select the events — the boundary
/// does that.
/// </summary>
public class SubscriptionDecisionId(string courseId, string studentId)
    : IDcbAggregateId<SubscriptionDecision>
{
    public string Id { get; } = $"{courseId}-{studentId}";

    /// <summary>
    /// Everything the decision reads, and nothing else — so a subscription to a different course by
    /// a different student never contends with this one.
    /// </summary>
    public TagQuery Boundary { get; } =
        TagQuery.AnyOf(new Tag("course", courseId), new Tag("student", studentId));
}
