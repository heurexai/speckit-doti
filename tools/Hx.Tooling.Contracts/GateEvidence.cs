namespace Hx.Tooling.Contracts;

public sealed record GateEvidence(
    string Kind,
    string Message,
    string? Path = null);
