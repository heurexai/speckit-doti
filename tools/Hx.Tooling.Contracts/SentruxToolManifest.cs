namespace Hx.Tooling.Contracts;

public sealed record SentruxToolManifest(
    string SourceRemote,
    string ReleaseTag,
    string SourceCommit,
    string Rid,
    string Sha256,
    IReadOnlyList<string> RequiredFeatures);
