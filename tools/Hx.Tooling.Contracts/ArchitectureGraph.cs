namespace Hx.Tooling.Contracts;

public sealed record ArchitectureGraph(
    IReadOnlyList<string> Projects,
    IReadOnlyList<string> Rules);
