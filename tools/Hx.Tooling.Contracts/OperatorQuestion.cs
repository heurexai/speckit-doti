namespace Hx.Tooling.Contracts;

/// <summary>One option in an operator question: a label plus its pros, cons, and downstream consequence.</summary>
public sealed record OperatorQuestionOption(
    string Label,
    IReadOnlyList<string> Pros,
    IReadOnlyList<string> Cons,
    string Consequence);

/// <summary>The recommended option (must name a real <see cref="OperatorQuestionOption.Label"/>) + the reasoning.</summary>
public sealed record OperatorRecommendation(string Option, string Reasoning);

/// <summary>An assumption behind the recommendation; <see cref="Verified"/> false ⇒ <see cref="WhatWouldVerify"/> required.</summary>
public sealed record OperatorAssumption(string Text, bool Verified, string? WhatWouldVerify);

/// <summary>Confidence: a level (High/Medium/Low) plus a one-line reason.</summary>
public sealed record OperatorConfidence(string Level, string Reason);

/// <summary>A premise the question rests on, with the evidence that confirms it.</summary>
public sealed record OperatorPremise(string Claim, string Evidence);

/// <summary>
/// The canonical operator-facing question (Layers B+C). <c>doti question check</c> validates it against
/// the rendered Operator-Question Protocol (Layer A) — the <b>same</b> fail-closed gate for
/// Codex and Claude — and a thin per-agent adapter maps it to each surface (Claude <c>AskUserQuestion</c>).
/// </summary>
public sealed record OperatorQuestion(
    int SchemaVersion,
    string Question,
    string WhyItMatters,
    IReadOnlyList<OperatorQuestionOption> Options,
    OperatorRecommendation Recommendation,
    IReadOnlyList<OperatorAssumption> Assumptions,
    OperatorConfidence Confidence,
    IReadOnlyList<OperatorPremise> Premises);
