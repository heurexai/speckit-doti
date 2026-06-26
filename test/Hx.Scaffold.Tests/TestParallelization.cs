using Xunit;

// Several tests in this assembly mutate process-global state: LocalReleaseRootTests switches the current working
// directory (Directory.SetCurrentDirectory), and StoreProvisionerTests sets/clears the ToolStore override env var.
// xUnit parallelizes test classes by default, so a class reading "." / the env var would race with a mutating class
// (an intermittent failure under load). Serialize the assembly — it is small and fast — so the state is deterministic.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
