namespace Hx.Tooling.Contracts;

/// <summary>JSON proof for Doti install/update: target classification plus exact path effects.</summary>
public sealed record DotiInstallResult(
    int SchemaVersion,
    StageOutcome Outcome,
    string Classification,
    bool TargetCreated,
    string? NextStep,
    IReadOnlyList<string> Rendered,
    IReadOnlyList<string> Copied,
    IReadOnlyList<DotiInstallPathEffect> Installed,
    IReadOnlyList<DotiInstallPathEffect> Preserved,
    IReadOnlyList<DotiInstallPathEffect> Removed,
    IReadOnlyList<DotiInstallPathEffect> Skipped,
    IReadOnlyList<DotiInstallPathEffect> Blocked,
    // 029 D8 (additive trailing-optional): the setup-config projection effect on the install path — the .doti-layer
    // fields written + any new-only field reported as ignored (FR-002). Null/omitted on the no-config path (SC-007).
    SetupConfigEffect? Setup = null);

/// <summary>029 FR-002: the install-path setup-config effect — the paths the Install-subset projection wrote +
/// the keys it ignored (new-only on install, or preserved).</summary>
public sealed record SetupConfigEffect(
    IReadOnlyList<string> Written,
    IReadOnlyList<DotiInstallPathEffect> Ignored,
    string? Persisted);

public sealed record DotiInstallPathEffect(string Path, string Reason);

public sealed record DotiPayloadCheckResult(
    int SchemaVersion,
    StageOutcome Outcome,
    string SourceRepoRoot,
    int CheckedCount,
    IReadOnlyList<DotiPayloadFileStatus> Files,
    IReadOnlyList<string> Drifted);

public sealed record DotiPayloadFileStatus(
    string SourcePath,
    string InstalledPath,
    string Kind,
    bool Matches,
    string? ExpectedSha256,
    string? ActualSha256,
    string? Reason);

/// <summary>Per-repo Doti integration descriptor written to <c>.doti/integration.json</c>.</summary>
public sealed record DotiIntegration(
    int SchemaVersion,
    string Name,
    string Profile,
    string Maturity,
    IReadOnlyList<string> Agents,
    string Context,
    string Workflow,
    string Constitution,
    DotiGeneratedBy GeneratedBy);

public sealed record DotiGeneratedBy(int Phase, string Mode);

/// <summary>Per-repo Doti init options written to <c>.doti/init-options.json</c>.</summary>
public sealed record DotiInitOptions(
    int SchemaVersion,
    string Profile,
    IReadOnlyList<string> Agents,
    string Maturity,
    string Source);
