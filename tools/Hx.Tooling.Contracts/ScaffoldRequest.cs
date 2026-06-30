using Hx.Tooling.Contracts.Setup;

namespace Hx.Tooling.Contracts;

public sealed record ScaffoldRequest(
    string Name,
    string Company,
    string OutputPath,
    string Profile,
    IReadOnlyList<string> Agents,
    // 029 D8/FR-008: additive trailing-optional — existing callers compile unchanged. Carries the resolved
    // operator setup config so ScaffoldNewRunner can project it after generation; null on the no-config path
    // (SC-007: the ScaffoldProof.Request.Setup JSON field is omitted/null, preserving the no-config proof shape).
    ResolvedSetupConfig? Setup = null);
