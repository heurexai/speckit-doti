namespace Hx.Tooling.Contracts;

public sealed record HygieneSkippedFile(
    string FilePath,
    string Reason);
