namespace Hx.Tooling.Contracts;

public sealed record VersionResult(
    string Version,
    string Increment,
    string Source);
