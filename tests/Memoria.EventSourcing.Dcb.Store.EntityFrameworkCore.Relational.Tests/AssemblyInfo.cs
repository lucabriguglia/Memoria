using Xunit;

// Every RelationalTestBase assigns the process-wide TypeBindings.EventTypeBindings in its
// constructor, so running the classes in parallel lets one class's setup land inside another's read.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
