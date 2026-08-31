using Xunit;

// Every TestBase-derived class assigns TypeBindings.EventTypeBindings in its constructor, and one
// test deliberately clears it to prove an unregistered event type is reported rather than skipped.
// Those are process-wide statics, so running the classes in parallel lets one class's setup — or
// that deliberate clear — land in the middle of another's read.
//
// Disabled for the whole assembly rather than per collection so a class added later is safe by
// default. These tests run against the in-memory provider in under a second, so the parallelism is
// worth nothing here.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
