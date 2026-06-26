namespace Hx.Tooling.Contracts;

/// <summary>An option in the describe tree: name, type, whether required, description, default.</summary>
public sealed record CliDescribeOption(
    string Name,
    string Type,
    bool Required,
    string? Description = null,
    string? Default = null);

/// <summary>A command in the describe tree, with its options and subcommands. <see cref="Mode"/> marks the
/// command installed-vs-source/developer (FR-022 / <see cref="CommandMode"/>); null until surfaced by T012/T045.</summary>
public sealed record CliDescribeCommand(
    string Name,
    string? Summary,
    IReadOnlyList<CliDescribeOption> Options,
    IReadOnlyList<CliDescribeCommand> Subcommands,
    string? Mode = null);

public sealed record CliDescribeWorkflow(
    string Name,
    IReadOnlyList<CliDescribeWorkflowStage> Stages);

public sealed record CliDescribeWorkflowStage(
    int Ordinal,
    string StageId,
    string CommandName,
    string SkillId,
    string DisplayTitle,
    string StageStatus,
    IReadOnlyList<string> NextStageIds,
    IReadOnlyList<CliDescribeWorkflowAlternateAction> AlternateActions,
    string NextStep);

public sealed record CliDescribeWorkflowAlternateAction(
    string Id,
    string Label,
    string CommandName,
    bool Optional);

/// <summary>
/// 007 T045 (FR-022/FR-042): the active repo tier + its gate ladder, surfaced by <c>describe --json</c> so an agent
/// learns which opinionated gates this repo enforces/skips. <see cref="Ladder"/> entries are the tier's declared step
/// modes (<see cref="GateLadderEntry"/>: step + <c>enforced</c>|<c>advisory</c>|<c>skip</c>); an undeclared step
/// defaults to <c>enforced</c>. <see cref="Tier"/> defaults to <c>workflow-only</c> when the repo declares no profile.
/// </summary>
public sealed record CliDescribeTier(
    string Tier,
    IReadOnlyList<GateLadderEntry> Ladder);

/// <summary>
/// The machine-readable capability description emitted by <c>describe --json</c> — the full command/option tree plus
/// the catalogs (the <see cref="ExitClasses"/> set + the error-code registry in <see cref="ErrorCodeCatalog"/>) so an
/// agent learns the whole tool in one call. The catalog is the set of <em>possible</em> codes, distinct from the
/// per-call <c>errors</c> ring on <see cref="CliResult"/>.
/// </summary>
public sealed record CliDescribe(
    int SchemaVersion,
    string Tool,
    string Version,
    CliDescribeCommand Root,
    IReadOnlyList<string> ExitClasses,
    IReadOnlyList<ErrorCodeEntry> ErrorCodeCatalog,
    CliDescribeWorkflow? Workflow = null,
    // 007 T004 (FR-022/FR-042): the active distribution channel + its update mechanism; null until surfaced by T012/T045.
    DistributionChannelInfo? Channel = null,
    // 007 T045 (FR-022/FR-042): the active repo tier + its gate ladder; null only when a declared profile fails closed.
    CliDescribeTier? Tier = null);
