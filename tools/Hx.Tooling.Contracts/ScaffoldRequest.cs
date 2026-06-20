namespace Hx.Tooling.Contracts;

public sealed record ScaffoldRequest(
    string Name,
    string Company,
    string OutputPath,
    string Profile,
    IReadOnlyList<string> Agents);
