// One app at a time, as in Bruinet's Todo suite: the Android head shares one emulator, and the
// headless head one dispatcher per app.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
