using Hx.Doti.Core.Setup;
using Hx.Scaffold.Cli;
using Hx.Tooling.Contracts.Setup;
using Xunit;

namespace Hx.Scaffold.Tests;

/// <summary>
/// 029 T016 (FR-005, SC-004): a scripted <c>--interactive</c> run (driven through the injectable
/// <see cref="ScriptedSetupConsole"/>) yields the SAME <see cref="ResolvedSetupConfig"/> and the SAME persisted
/// <c>.doti/setup.json</c> as the equivalent <c>--config</c> file — proving the wizard is a 1:1 front-end onto the
/// <c>--config</c> resolve+project path (no second codepath). No TTY required.
/// </summary>
public sealed class SetupWizardIntegrationTests
{
    // The operator intent expressed two ways: as a --config JSON, and as the per-key answers the wizard collects.
    private const string EquivalentConfigJson = """
    {
      "schemaVersion": 1,
      "identity": {
        "name": "Acme.Widget",
        "company": "Acme",
        "description": "The Acme widget CLI.",
        "license": "Apache-2.0"
      },
      "versioning": { "nextVersion": "2.3.4" },
      "publish": { "enabled": true, "owner": "acme", "repo": "widget" },
      "constitution": { "domainPrinciples": "Ship guarded widgets." }
    }
    """;

    private static readonly IReadOnlyDictionary<string, string> Answers = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [SetupKeys.IdentityName] = "Acme.Widget",
        [SetupKeys.IdentityCompany] = "Acme",
        [SetupKeys.IdentityDescription] = "The Acme widget CLI.",
        [SetupKeys.IdentityLicense] = "Apache-2.0",
        [SetupKeys.VersioningNextVersion] = "2.3.4",
        [SetupKeys.PublishEnabled] = "true",
        [SetupKeys.PublishOwner] = "acme",
        [SetupKeys.PublishRepo] = "widget",
        [SetupKeys.ConstitutionDomainPrinciples] = "Ship guarded widgets.",
    };

    [Fact]
    public void Scripted_wizard_yields_the_same_resolved_config_as_the_equivalent_config_file()
    {
        // The --config path: validate + resolve (New audience).
        SetupValidationResult validation = SetupConfigSchema.ValidateRaw(EquivalentConfigJson, out SetupConfig? fileConfig);
        Assert.True(validation.Ok, string.Join("; ", validation.Errors.Select(e => $"{e.Field}: {e.Message}")));
        ResolvedSetupConfig fromConfig = SetupConfigResolver.Resolve(fileConfig, flags: null, SetupAudience.New);

        // The --interactive path: drive the REAL wizard with scripted input + no flags, then resolve identically.
        var flags = new SetupFlagOverrides();
        var console = new ScriptedSetupConsole(ScriptFor(SetupAudience.New, flags));
        SetupConfig wizardConfig = SetupWizard.Run(console, SetupAudience.New, flags);
        ResolvedSetupConfig fromWizard = SetupConfigResolver.Resolve(
            wizardConfig, flags, SetupAudience.New, ConfigSource.Interactive);

        // Every resolved key carries the same effective VALUE (the wizard's source is Interactive vs ConfigFile by
        // design — that distinction is what `config show` surfaces — so value/custom equality is the 1:1 contract).
        foreach (SetupKey key in SetupKeys.All)
        {
            if (!SetupConfigResolver.Applies(key.Audience, SetupAudience.New))
            {
                continue;
            }

            Assert.Equal(fromConfig.ValueOrDefault(key.Id), fromWizard.ValueOrDefault(key.Id));
            Assert.Equal(fromConfig.IsCustom(key.Id), fromWizard.IsCustom(key.Id));
        }
    }

    [Fact]
    public void Scripted_wizard_persists_the_same_setup_json_as_the_config_file()
    {
        string configRepo = NewRepo();
        string wizardRepo = NewRepo();
        try
        {
            SetupConfigSchema.ValidateRaw(EquivalentConfigJson, out SetupConfig? fileConfig);
            ResolvedSetupConfig fromConfig = SetupConfigResolver.Resolve(fileConfig, flags: null, SetupAudience.New);
            SetupConfigStore.WriteFromResolved(configRepo, fromConfig);

            var flags = new SetupFlagOverrides();
            var console = new ScriptedSetupConsole(ScriptFor(SetupAudience.New, flags));
            SetupConfig wizardConfig = SetupWizard.Run(console, SetupAudience.New, flags);
            ResolvedSetupConfig fromWizard = SetupConfigResolver.Resolve(
                wizardConfig, flags, SetupAudience.New, ConfigSource.Interactive);
            SetupConfigStore.WriteFromResolved(wizardRepo, fromWizard);

            // The persisted INTENT (the tracked .doti/setup.json) is byte-identical between the two input paths.
            string configJson = File.ReadAllText(Path.Combine(configRepo, ".doti", "setup.json"));
            string wizardJson = File.ReadAllText(Path.Combine(wizardRepo, ".doti", "setup.json"));
            Assert.Equal(configJson, wizardJson);

            // And both round-trip back to the same intent shape.
            SetupConfig? configRead = SetupConfigStore.Read(configRepo);
            SetupConfig? wizardRead = SetupConfigStore.Read(wizardRepo);
            Assert.Equal("Acme.Widget", wizardRead!.Identity!.Name);
            Assert.Equal(configRead!.Versioning!.NextVersion, wizardRead.Versioning!.NextVersion);
            Assert.True(wizardRead.Publish!.Enabled);
        }
        finally
        {
            ForceDelete(configRepo);
            ForceDelete(wizardRepo);
        }
    }

    [Fact]
    public void Wizard_review_step_renders_the_effective_config_before_returning()
    {
        // FR-005: the wizard ends with a Review render (config show human) the operator confirms before any file write.
        var flags = new SetupFlagOverrides();
        var console = new ScriptedSetupConsole(ScriptFor(SetupAudience.New, flags));
        SetupWizard.Run(console, SetupAudience.New, flags);

        string output = string.Join("\n", console.Output);
        Assert.Contains("Review", output);
        Assert.Contains("Acme.Widget", output);          // the supplied name appears in the review table
        Assert.Matches(@"\d+ custom · \d+ default", output);
    }

    /// <summary>Replay the answers in the wizard's own iteration order (skipping flag-provided + gated-off prompts), so
    /// the <see cref="ScriptedSetupConsole"/> dequeues them exactly as <see cref="SetupWizard.Run"/> reads them.</summary>
    private static IReadOnlyList<string> ScriptFor(SetupAudience audience, SetupFlagOverrides flags)
    {
        var script = new List<string>();
        var soFar = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (SetupPromptDefinition prompt in SetupPromptDefinitions.All)
        {
            if (!SetupConfigResolver.Applies(prompt.Audience, audience) || FlagProvides(prompt.Key, flags))
            {
                continue;
            }

            if (prompt.EnabledWhen is { } gate
                && !(soFar.TryGetValue(gate.Key, out string? v) && string.Equals(v, gate.EqualsValue, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            string answer = Answers.TryGetValue(prompt.Key, out string? a) ? a : string.Empty; // blank = accept default
            script.Add(answer);
            if (answer.Length > 0)
            {
                soFar[prompt.Key] = answer;
            }
        }

        return script;
    }

    private static bool FlagProvides(string key, SetupFlagOverrides flags) => key switch
    {
        SetupKeys.IdentityName => flags.Name is not null,
        SetupKeys.IdentityCompany => flags.Company is not null,
        SetupKeys.Agents => flags.Agents is { Count: > 0 },
        _ => false,
    };

    private static string NewRepo()
    {
        string dir = Path.Combine(Path.GetTempPath(), "hx-wizard-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void ForceDelete(string dir)
    {
        if (!Directory.Exists(dir))
        {
            return;
        }

        try { Directory.Delete(dir, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
