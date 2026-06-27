using System.CommandLine;
using Hx.Cli.Kernel;

namespace Hx.Runner.Cli;

public static partial class RunnerCommandFactory
{
    private static void AddDoti(RootCommand rootCommand, CliMeta meta)
    {
        Command dotiCommand = new("doti", "Doti self-hosting: render skills, cycle state, hooks, operator-question protocol.");
        AddDotiRenderSkills(dotiCommand, meta);
        AddDotiInstall(dotiCommand, meta);
        AddDotiPayload(dotiCommand, meta);
        AddDotiCycle(dotiCommand, meta);
        AddDotiReviewContext(dotiCommand, meta);
        AddDotiConstitution(dotiCommand, meta);
        AddDotiDriftCandidates(dotiCommand, meta);
        AddDotiTaskHash(dotiCommand, meta);
        AddDotiInstallHooks(dotiCommand, meta);
        AddDotiQuestion(dotiCommand, meta);
        AddDotiBug(dotiCommand, meta);
        AddDotiConverge(dotiCommand, meta);
        rootCommand.Subcommands.Add(dotiCommand);
    }

    // 008 FR-025: project the change set into arch-review lens applicability (change/review context).
    private static void AddDotiReviewContext(Command dotiCommand, CliMeta meta)
    {
        Command command = new("review-context",
            "Project the change set into arch-review lens applicability (FR-025): change categories + which lenses apply vs skip.");
        Option<string> baseRef = new("--base") { Description = "Base ref for the change set (default: the cycle base, else HEAD).", DefaultValueFactory = _ => "" };
        Option<string> repo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(baseRef);
        command.Options.Add(repo);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "doti review-context",
            () => RunnerCommands.DotiReviewContext(meta, parseResult.GetValue(repo)!, parseResult.GetValue(baseRef)!),
            forceJson: CliApp.ForceJson(parseResult, json)));
        dotiCommand.Subcommands.Add(command);
    }

    // 009 FR-006: emit the project constitution (§2 by default) — the carrier for the codified plan/arch-review
    // fresh-context injection + the on-demand agent tool. Surface-and-proceed when absent (FR-016).
    private static void AddDotiConstitution(Command dotiCommand, CliMeta meta)
    {
        Command command = new("constitution",
            "Emit the project constitution (FR-006): §2 project declarations by default, --section full for the whole file. Carrier for the codified plan/arch-review fresh-context injection + the on-demand agent tool; surface-and-proceed when absent.");
        Option<string> repo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
        Option<string> section = new("--section") { Description = "Which layer to emit: §2 (default) or full.", DefaultValueFactory = _ => "§2" };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(repo);
        command.Options.Add(section);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "doti constitution",
            () => RunnerCommands.DotiConstitution(meta, parseResult.GetValue(repo)!, parseResult.GetValue(section)!),
            forceJson: CliApp.ForceJson(parseResult, json)));
        dotiCommand.Subcommands.Add(command);
    }

    // 008 M-5/FR-042: the advisory semantic drift finder, composed into the packed tool (mirroring `hx impact`).
    // Never gating — when no model/engine is provisioned it skips.
    private static void AddDotiDriftCandidates(Command dotiCommand, CliMeta meta)
    {
        Command command = new("drift-candidates",
            "Advisory semantic drift candidates over the change set (FR-042; never gating). Reports the active engine; absence of candidates is NOT a clean-bill signal.");
        Option<string> repo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
        Option<string> baseRef = new("--base") { Description = "Base ref for the change set (default: HEAD).", DefaultValueFactory = _ => "HEAD" };
        Option<string> modelRoot = new("--model-root") { Description = "Local LLM model root (else config.llmModelRoot, else HEUREX_LLM_ROOT).", DefaultValueFactory = _ => "" };
        Option<double> threshold = new("--threshold") { Description = "Cosine threshold (else the engine default).", DefaultValueFactory = _ => 0 };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(repo);
        command.Options.Add(baseRef);
        command.Options.Add(modelRoot);
        command.Options.Add(threshold);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "doti drift-candidates",
            () => RunnerCommands.DotiDriftCandidates(meta, parseResult.GetValue(repo)!, parseResult.GetValue(baseRef)!,
                parseResult.GetValue(modelRoot)!, parseResult.GetValue(threshold)),
            forceJson: CliApp.ForceJson(parseResult, json)));
        dotiCommand.Subcommands.Add(command);
    }

    // 007 T038 (FR-039): brownfield/drift reconciliation — report the requirement coverage gap.
    private static void AddDotiConverge(Command dotiCommand, CliMeta meta)
    {
        Command command = new("converge",
            "Brownfield/drift reconciliation: report the spec requirements (FR/SC) not covered by any task.");
        Option<string> spec = new("--spec") { Description = "Path to the feature spec markdown.", DefaultValueFactory = _ => "" };
        Option<string> tasks = new("--tasks") { Description = "Path to the feature tasks markdown.", DefaultValueFactory = _ => "" };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(spec);
        command.Options.Add(tasks);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "doti converge",
            () => RunnerCommands.Converge(meta, parseResult.GetValue(spec)!, parseResult.GetValue(tasks)!),
            forceJson: CliApp.ForceJson(parseResult, json)));
        dotiCommand.Subcommands.Add(command);
    }

    // 007 T033 (FR-034): the enforced bug mini-cycle as thin CLI over BugCycleService.
    private static void AddDotiBug(Command dotiCommand, CliMeta meta)
    {
        Command bug = new("bug",
            "Enforced bug mini-cycle: assess (read-only) -> fix (bound to a confirmed assessment) -> test (honest verification).");
        bug.Subcommands.Add(BugAssessCommand(meta));
        bug.Subcommands.Add(BugFixCommand(meta));
        bug.Subcommands.Add(BugTestCommand(meta));
        dotiCommand.Subcommands.Add(bug);
    }

    private static Command BugAssessCommand(CliMeta meta)
    {
        Command command = new("assess", "Record the read-only verdict/severity/remediation assessment for a bug (writes no code).");
        Option<string> repo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
        Option<string> bugId = new("--bug") { Description = "Bug id (e.g. 001-null-ref).", DefaultValueFactory = _ => "" };
        Option<string> verdict = new("--verdict") { Description = "confirmed | rejected | needs-info.", DefaultValueFactory = _ => "confirmed" };
        Option<string> severity = new("--severity") { Description = "critical | high | medium | low.", DefaultValueFactory = _ => "medium" };
        Option<string> remediation = new("--remediation") { Description = "The remediation the fix must address.", DefaultValueFactory = _ => "" };
        Option<string> summary = new("--summary") { Description = "One-line bug summary.", DefaultValueFactory = _ => "" };
        Option<bool> json = CliApp.JsonOption();
        foreach (Option option in new Option[] { repo, bugId, verdict, severity, remediation, summary, json })
        {
            command.Options.Add(option);
        }

        command.SetAction(parseResult => CliHost.Run(meta, "doti bug assess",
            () => RunnerCommands.BugAssess(meta, parseResult.GetValue(repo)!, parseResult.GetValue(bugId)!,
                parseResult.GetValue(verdict)!, parseResult.GetValue(severity)!, parseResult.GetValue(remediation)!,
                parseResult.GetValue(summary)!),
            forceJson: CliApp.ForceJson(parseResult, json)));
        return command;
    }

    private static Command BugFixCommand(CliMeta meta)
    {
        Command command = new("fix", "Record a fix bound to the bug's confirmed assessment; fails closed if no assessment or unbound.");
        Option<string> repo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
        Option<string> bugId = new("--bug") { Description = "Bug id (e.g. 001-null-ref).", DefaultValueFactory = _ => "" };
        Option<string> summary = new("--summary") { Description = "One-line fix summary.", DefaultValueFactory = _ => "" };
        Option<string> changed = new("--changed") { Description = "Comma-separated changed paths.", DefaultValueFactory = _ => "" };
        Option<bool> json = CliApp.JsonOption();
        foreach (Option option in new Option[] { repo, bugId, summary, changed, json })
        {
            command.Options.Add(option);
        }

        command.SetAction(parseResult => CliHost.Run(meta, "doti bug fix",
            () => RunnerCommands.BugFix(meta, parseResult.GetValue(repo)!, parseResult.GetValue(bugId)!,
                parseResult.GetValue(summary)!, parseResult.GetValue(changed)!),
            forceJson: CliApp.ForceJson(parseResult, json)));
        return command;
    }

    private static Command BugTestCommand(CliMeta meta)
    {
        Command command = new("test", "Record the honest verification of the fix (a pass requires evidence; no over-claiming).");
        Option<string> repo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
        Option<string> bugId = new("--bug") { Description = "Bug id (e.g. 001-null-ref).", DefaultValueFactory = _ => "" };
        Option<string> outcome = new("--outcome") { Description = "pass | fail.", DefaultValueFactory = _ => "fail" };
        Option<string> evidence = new("--evidence") { Description = "Evidence for the verdict (required for a pass).", DefaultValueFactory = _ => "" };
        Option<bool> json = CliApp.JsonOption();
        foreach (Option option in new Option[] { repo, bugId, outcome, evidence, json })
        {
            command.Options.Add(option);
        }

        command.SetAction(parseResult => CliHost.Run(meta, "doti bug test",
            () => RunnerCommands.BugTest(meta, parseResult.GetValue(repo)!, parseResult.GetValue(bugId)!,
                parseResult.GetValue(outcome)!, parseResult.GetValue(evidence)!),
            forceJson: CliApp.ForceJson(parseResult, json)));
        return command;
    }

    private static void AddDotiRenderSkills(Command dotiCommand, CliMeta meta)
    {
        Command command = new("render-skills",
            "Render installed Codex/Claude skills + shared agent context from one source; --check reports drift (fail closed).");
        Option<string> repo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
        Option<string> agents = new("--agents") { Description = "Comma-separated agents (codex,claude).", DefaultValueFactory = _ => "codex,claude" };
        Option<bool> check = new("--check") { Description = "Check for drift instead of writing.", DefaultValueFactory = _ => false };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(repo);
        command.Options.Add(agents);
        command.Options.Add(check);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "doti render-skills",
            () => RunnerCommands.DotiRenderSkills(meta, parseResult.GetValue(repo)!,
                parseResult.GetValue(agents)!, parseResult.GetValue(check)),
            forceJson: CliApp.ForceJson(parseResult, json)));
        dotiCommand.Subcommands.Add(command);
    }

    private static void AddDotiPayload(Command dotiCommand, CliMeta meta)
    {
        Command payloadCommand = new("payload", "Doti payload parity tooling.");
        Command checkCommand = new("check", "Check scaffold-installed Doti payload parity against this repo's source assets.");
        Option<string> repo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
        Option<bool> json = CliApp.JsonOption();
        checkCommand.Options.Add(repo);
        checkCommand.Options.Add(json);
        checkCommand.SetAction(parseResult => CliHost.Run(meta, "doti payload check",
            () => RunnerCommands.DotiPayloadCheck(meta, parseResult.GetValue(repo)!),
            forceJson: CliApp.ForceJson(parseResult, json)));
        payloadCommand.Subcommands.Add(checkCommand);
        dotiCommand.Subcommands.Add(payloadCommand);
    }

    private static void AddDotiInstall(Command dotiCommand, CliMeta meta)
    {
        Command command = new("install", "Install, repair, migrate, or update Doti workflow assets in a --repo target (version-aware reconciliation; operator edits preserved).");
        Option<string?> repo = new("--repo") { Description = "Target repository root to install into. Required; the command never defaults to the current directory." };
        Option<string> agents = new("--agents") { Description = "Comma-separated agents (codex,claude).", DefaultValueFactory = _ => "codex,claude" };
        Option<bool> force = new("--force") { Description = "Replace modified/unknown legacy Doti-owned assets during migration." };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(repo);
        command.Options.Add(agents);
        command.Options.Add(force);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "doti install",
            () => RunnerCommands.DotiInstall(meta, parseResult.GetValue(repo), parseResult.GetValue(agents)!, parseResult.GetValue(force)),
            forceJson: CliApp.ForceJson(parseResult, json)));
        dotiCommand.Subcommands.Add(command);
    }

    private static void AddDotiInstallHooks(Command dotiCommand, CliMeta meta)
    {
        Command command = new("install-hooks", "Install the doti insurance pre-commit hook into this repo's (untracked) git hooks dir.");
        Option<string> repo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(repo);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "doti install-hooks",
            () => RunnerCommands.InstallHooks(meta, parseResult.GetValue(repo)!),
            forceJson: CliApp.ForceJson(parseResult, json)));
        dotiCommand.Subcommands.Add(command);
    }

    private static void AddDotiTaskHash(Command dotiCommand, CliMeta meta)
    {
        Command taskHashCommand = new("task-hash", "Task-completion hash tooling.");
        Command stampCommand = new("stamp", "Stamp canonical doti-task-hash markers for checked tasks; refuse unchecked tasks.");
        Option<string> repo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
        Option<string> feature = new("--feature") { Description = "Numbered feature slug. Defaults to the active Doti cycle feature.", DefaultValueFactory = _ => "" };
        Option<bool> json = CliApp.JsonOption();
        stampCommand.Options.Add(repo);
        stampCommand.Options.Add(feature);
        stampCommand.Options.Add(json);
        stampCommand.SetAction(parseResult => CliHost.Run(meta, "doti task-hash stamp",
            () => RunnerCommands.TaskHashStamp(meta, parseResult.GetValue(repo)!, parseResult.GetValue(feature)!),
            forceJson: CliApp.ForceJson(parseResult, json)));
        taskHashCommand.Subcommands.Add(stampCommand);
        dotiCommand.Subcommands.Add(taskHashCommand);
    }

    private static void AddDotiQuestion(Command dotiCommand, CliMeta meta)
    {
        Command questionCommand = new("question", "Operator-question protocol tooling.");
        Command checkCommand = new("check", "Fail-closed: validate an OperatorQuestion JSON file against the protocol.");
        Option<string> file = new("--file") { Description = "Path to the OperatorQuestion JSON file.", DefaultValueFactory = _ => "" };
        Option<bool> json = CliApp.JsonOption();
        checkCommand.Options.Add(file);
        checkCommand.Options.Add(json);
        checkCommand.SetAction(parseResult => CliHost.Run(meta, "doti question check",
            () => RunnerCommands.QuestionCheck(meta, parseResult.GetValue(file)!),
            forceJson: CliApp.ForceJson(parseResult, json)));
        questionCommand.Subcommands.Add(checkCommand);
        dotiCommand.Subcommands.Add(questionCommand);
    }
}
