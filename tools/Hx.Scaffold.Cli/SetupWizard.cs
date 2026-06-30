using Hx.Doti.Core.Setup;
using Hx.Tooling.Contracts.Setup;

namespace Hx.Scaffold.Cli;

/// <summary>
/// 029 FR-005/D4: the <c>--interactive</c> setup wizard — a DUMB ITERATOR over the pure
/// <see cref="SetupPromptDefinitions"/> behind an injectable <see cref="ISetupConsole"/>. It echoes each prompt's
/// "what it affects", collects answers into a <see cref="SetupConfig"/> (the same shape <c>--config</c> binds), and
/// ends with a Review step that renders the effective config (FR-004 human render) before any file is written —
/// then the caller re-enters the identical <c>--config</c> resolve+project path (so the two inputs are provably 1:1).
/// Conditional branches are DATA (<see cref="SetupPromptDefinition.EnabledWhen"/>), never CLI branching. The neutral
/// name avoids every forbidden cliSurfaceConfinement suffix.
/// </summary>
public static class SetupWizard
{
    public static SetupConfig Run(ISetupConsole console, SetupAudience audience, SetupFlagOverrides flags)
    {
        console.WriteLine("Setup wizard — answer each prompt or press Enter to accept the default.");
        var answers = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (SetupPromptDefinition prompt in SetupPromptDefinitions.All)
        {
            if (!SetupConfigResolver.Applies(prompt.Audience, audience))
            {
                continue;
            }

            if (FlagAlreadyProvides(prompt.Key, flags))
            {
                continue; // an explicit flag wins; the wizard does not re-ask it.
            }

            if (prompt.EnabledWhen is { } gate && !IsGateMet(gate, answers))
            {
                continue; // conditional branch not taken (e.g. publish sub-questions when publish=false).
            }

            console.WriteLine($"  {prompt.Question} [{DisplayDefault(prompt.Default)}]");
            console.WriteLine($"    ↳ {prompt.WhatItAffects}");
            string? raw = console.ReadLine();
            string answer = (raw ?? string.Empty).Trim();
            if (answer.Length > 0)
            {
                answers[prompt.Key] = answer;
            }
        }

        SetupConfig config = SetupConfigBuilder.FromAnswers(answers);
        RenderReview(console, config, flags, audience);
        return config;
    }

    private static void RenderReview(ISetupConsole console, SetupConfig config, SetupFlagOverrides flags, SetupAudience audience)
    {
        ResolvedSetupConfig resolved = SetupConfigResolver.Resolve(config, flags, audience, ConfigSource.Interactive);
        console.WriteLine("");
        console.WriteLine("Review — the effective configuration:");
        console.WriteLine(SetupConfigTableFormatter.FormatHuman(resolved));
    }

    private static bool FlagAlreadyProvides(string key, SetupFlagOverrides flags) => key switch
    {
        SetupKeys.IdentityName => flags.Name is not null,
        SetupKeys.IdentityCompany => flags.Company is not null,
        SetupKeys.Agents => flags.Agents is { Count: > 0 },
        _ => false,
    };

    private static bool IsGateMet(SetupEnabledWhen gate, IReadOnlyDictionary<string, string> answers) =>
        answers.TryGetValue(gate.Key, out string? value) && string.Equals(value, gate.EqualsValue, StringComparison.OrdinalIgnoreCase);

    private static string DisplayDefault(string value) => value.Length == 0 ? "blank" : value;
}
