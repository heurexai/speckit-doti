namespace Hx.Tooling.Contracts;

/// <summary>
/// JSON proof for <c>Hx.Scaffold.Cli new</c>: the user request, the resolved template invocation,
/// and the first-smoke gate proof for the generated repo. <see cref="Outcome"/> is the overall
/// result (Pass only when generation, finishing, and the smoke all pass).
/// </summary>
public sealed record ScaffoldProof(
    int SchemaVersion,
    StageOutcome Outcome,
    ScaffoldRequest Request,
    TemplateInvocation Template,
    GateProof Smoke);
