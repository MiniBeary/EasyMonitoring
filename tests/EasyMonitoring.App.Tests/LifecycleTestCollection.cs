namespace EasyMonitoring.App.Tests;

// Blocking hardware-read gates and real subprocesses must not starve each other's workers on small CI runners.
[CollectionDefinition(nameof(LifecycleTestCollection), DisableParallelization = true)]
public sealed class LifecycleTestCollection { }
