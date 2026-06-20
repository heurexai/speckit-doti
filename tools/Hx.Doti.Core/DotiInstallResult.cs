using Hx.Tooling.Contracts;

namespace Hx.Doti.Core;

/// <summary>JSON proof for <c>doti install</c>: what was rendered and which static sets were copied.</summary>
public sealed record DotiInstallResult(
    int SchemaVersion,
    StageOutcome Outcome,
    IReadOnlyList<string> Rendered,
    IReadOnlyList<string> Copied);

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
