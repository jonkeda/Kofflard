using Xunit;

// One app at a time: the desktop and the phone scenarios each launch their own, and on a device head two
// windows (or two sessions) would take each other's input.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
