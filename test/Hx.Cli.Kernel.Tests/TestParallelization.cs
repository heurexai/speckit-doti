using Xunit;

// Several tests in this assembly capture the process-global Console.Out (the renderer's human/help path writes
// there). xUnit parallelizes test classes by default, so two console-capturing classes would race on Console.Out.
// Serialize the assembly — it is small and fast — so the captures are deterministic.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
