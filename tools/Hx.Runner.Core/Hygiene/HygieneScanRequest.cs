using Hx.Tooling.Contracts;

namespace Hx.Runner.Core.Hygiene;

public sealed record HygieneScanRequest(
    string RepositoryRoot,
    HygieneScope Scope,
    HygieneSource Source,
    string? BaseRef = null,
    string? HeadRef = null);
