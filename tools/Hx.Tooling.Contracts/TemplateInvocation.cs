namespace Hx.Tooling.Contracts;

public sealed record TemplateInvocation(
    string Identity,
    string Source,
    IReadOnlyDictionary<string, string> Symbols,
    StageOutcome Outcome);
