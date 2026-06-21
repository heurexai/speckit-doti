using System.Text;
using System.Text.Json;
using Hx.Cli.Kernel;
using Hx.Cycle.Core;
using Hx.Doti.Core;
using Hx.Gate.Core;
using Hx.Runner.Core;
using Hx.Runner.Core.ArchitectureGate;
using Hx.Runner.Core.Gitleaks;
using Hx.Runner.Core.Hygiene;
using Hx.Runner.Core.Platform;
using Hx.Runner.Core.Repository;
using Hx.Runner.Core.Tools;
using Hx.Security.Core;
using Hx.Sentrux.Core;
using Hx.Tooling.Contracts;
using Hx.Version.Core;

namespace Hx.Runner.Cli;

/// <summary>
/// The Runner CLI's command bodies: each maps a deterministic core result onto the <see cref="CliResult"/> envelope.
/// Kept out of <c>Program.cs</c> wiring so the mappings are unit-testable in-process (the repo's "test the core, not
/// the exe" convention). Exit semantics: a check/gate that did not pass ⇒ <see cref="ExitClass.Validation"/>; a tool
/// integrity verification failure ⇒ <see cref="ExitClass.Integrity"/>; bad invocation ⇒ <see cref="ExitClass.Usage"/>;
/// an unexpected exception fail-closes (the host maps it to Internal).
/// </summary>
public static class RunnerCommands
{
    private static readonly JsonSerializerOptions JsonOptions = JsonContractSerializerOptions.Create();

    // ---- bootstrap / platform ----

    public static CliResult BootstrapProof(CliMeta meta) =>
        CliResults.Ok(meta, "bootstrap-proof", "Bootstrap advisory proof.", GateProofFactory.BootstrapAdvisoryProof());

    public static CliResult PlatformProbe(CliMeta meta) =>
        CliResults.Ok(meta, "platform probe", "Cross-platform diagnostics.", CrossPlatformProbe.Create());

    // ---- hygiene ----

    public static CliResult HygieneScan(CliMeta meta, string repo, string scopeRaw, string sourceRaw, string? baseRef, string? headRef)
    {
        bool isAll = string.Equals(scopeRaw, "all", StringComparison.OrdinalIgnoreCase);
        HygieneScope scope = isAll ? HygieneScope.All : HygieneScope.Changed;
        HygieneSource source = scope == HygieneScope.All
            ? HygieneSource.WorkingTree
            : string.Equals(sourceRaw, "range", StringComparison.OrdinalIgnoreCase) ? HygieneSource.Range : HygieneSource.Staged;

        HygieneScanResult result = HygieneScanner.Scan(new HygieneScanRequest(repo, scope, source, baseRef, headRef));
        return CliResults.FromStage(meta, "hygiene scan", result.Outcome,
            $"{scope}/{source}: {result.ScannedFileCount} file(s), {result.Findings.Count} finding(s).", result);
    }

    public static CliResult GitleaksVerify(CliMeta meta, string repo) =>
        Verify(meta, "hygiene gitleaks verify", GitleaksManifestValidator.Verify(repo, Rid()));

    public static CliResult GitleaksUpdateCheck(CliMeta meta, string repo) =>
        CliResults.Ok(meta, "hygiene gitleaks update-check", "Gitleaks upstream update check.", GitleaksUpdateChecker.Check(repo));

    public static CliResult GitleaksRenderConfig(CliMeta meta, string repo)
    {
        HygienePolicy policy = HygienePolicyLoader.Load(repo, out _);
        string toml = GitleaksConfigRenderer.Render(policy);
        string outPath = RepositoryPathResolver.ResolveInside(repo, "tools/gitleaks/config/gitleaks.toml").FullPath;
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        File.WriteAllText(outPath, toml);
        return CliResults.Ok(meta, "hygiene gitleaks render-config", $"Rendered gitleaks.toml ({toml.Length} chars).",
            new { path = outPath, chars = toml.Length }, effects: [new CliEffect("write", outPath, $"{toml.Length} chars")]);
    }

    // ---- sentrux ----

    public static CliResult SentruxVerify(CliMeta meta, string repo)
    {
        SentruxPolicy policy = SentruxPolicyLoader.Load(repo, out _);
        return Verify(meta, "sentrux verify", SentruxManifestValidator.Verify(repo, Rid(), policy));
    }

    public static CliResult SentruxBaseline(CliMeta meta, string repo)
    {
        SentruxBaselineResult result = SentruxBaselineRunner.Save(repo);
        return CliResults.FromStage(meta, "sentrux baseline", result.Outcome, "Sentrux baseline.", result);
    }

    public static CliResult SentruxCheck(CliMeta meta, string repo)
    {
        SentruxCheckResult result = SentruxChecker.Check(repo);
        return CliResults.FromStage(meta, "sentrux check", result.Outcome,
            $"signal={result.QualitySignal}; regression={result.RegressionOutcome}.", result);
    }

    // ---- doti render / install ----

    public static CliResult DotiRenderSkills(CliMeta meta, string repo, string agentsCsv, bool check)
    {
        if (!TryParseAgents(agentsCsv, out List<DotiAgentTarget> agents, out string? error))
        {
            return Usage(meta, "doti render-skills", error!);
        }

        DotiRenderResult result = DotiRenderer.Render(repo, agents, check);
        string summary = result.Outcome == StageOutcome.Pass
            ? (check ? "No skill drift." : "Skills rendered.")
            : "Skill drift: " + string.Join(", ", result.Drifted);
        return CliResults.FromStage(meta, "doti render-skills", result.Outcome, summary, result);
    }

    public static CliResult DotiInstall(CliMeta meta, string targetRepo, string agentsCsv)
    {
        if (!TryParseAgents(agentsCsv, out List<DotiAgentTarget> agents, out string? error))
        {
            return Usage(meta, "doti install", error!);
        }

        string target = Path.GetFullPath(targetRepo);
        string? source = FindDotiSource(Directory.GetCurrentDirectory());
        if (source is null)
        {
            return Usage(meta, "doti install", "Could not locate doti/core/skills.json above the current directory.");
        }

        string repoName = Path.GetFileName(target.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        DotiInstallResult result = DotiInstaller.Install(source, target, agents, repoName);
        return CliResults.FromStage(meta, "doti install", result.Outcome, $"Doti install into {target}.", result);
    }

    // ---- doti cycle ----

    public static CliResult CycleStamp(CliMeta meta, string repo, string stage, string feature, string baseRef)
    {
        if (string.IsNullOrWhiteSpace(stage))
        {
            return Usage(meta, "doti cycle stamp", "--stage is required.");
        }

        CycleState state = new CycleService(repo).Stamp(
            stage,
            string.IsNullOrWhiteSpace(feature) ? null : feature,
            string.IsNullOrWhiteSpace(baseRef) ? null : baseRef);
        return CliResults.Ok(meta, "doti cycle stamp", $"Stamped stage '{stage}'.", state);
    }

    public static CliResult CycleStatus(CliMeta meta, string repo) =>
        // Non-enforcing: a STALE stage is reported (in data), not gated.
        CliResults.Ok(meta, "doti cycle status", "Cycle status.", new CycleService(repo).Status());

    public static CliResult CycleCheck(CliMeta meta, string repo, string stage)
    {
        if (string.IsNullOrWhiteSpace(stage))
        {
            return Usage(meta, "doti cycle check", "--stage is required.");
        }

        CycleCheckReport report = new CycleService(repo).Check(stage);
        if (report.Passed)
        {
            return CliResults.Ok(meta, "doti cycle check", $"All prerequisites for '{stage}' are stamped + fresh.", report);
        }

        List<Diagnostic> errors = report.Prerequisites
            .Where(p => !p.Ok)
            .Select(p => Diag.Of(ErrorCodes.Validation_Failed, $"{p.Stage}: {p.Status}" + (p.Reason is { } r ? $" ({r})" : ""), target: p.Stage))
            .ToList();
        return CliResults.Fail(meta, "doti cycle check", ExitClass.Validation, errors,
            $"Prerequisites for '{stage}' are not all fresh.", report);
    }

    public static CliResult CycleCommit(CliMeta meta, string repo, string message)
    {
        CycleCommitResult result = new CycleService(repo).Commit(message);
        if (result.Committed)
        {
            return CliResults.Ok(meta, "doti cycle commit", $"Committed {result.CommitSha}.", result,
                effects: [new CliEffect("commit", result.CommitSha ?? "HEAD", "sanctioned commit")]);
        }

        // A fail-closed refusal (stale proof, dirty staged scope, missing prerequisite) is a blocked state requiring
        // operator action — not a multiple-choice decision, so `requiresOperator` carries diagnostics + next actions
        // (no fabricated OperatorQuestion).
        List<Diagnostic> errors = result.Reasons
            .Select(r => Diag.Of(ErrorCodes.Validation_Failed, r))
            .ToList();
        return CliResults.Blocked(meta, "doti cycle commit", ExitClass.Validation, errors,
            "Commit refused: the sanctioned-commit prerequisites are not all met.", result,
            nextActions:
            [
                new CliNextAction("Resolve the listed blockers, then retry", "The commit chokepoint is fail-closed.", "doti cycle commit --message \"…\""),
                new CliNextAction("Re-run the gate if its proof is stale", "A fresh passing gate proof is required.", "gate run --profile normal"),
            ]);
    }

    public static CliResult PrecommitGuard(CliMeta meta)
    {
        if (PrecommitGuard_IsSanctioned())
        {
            return CliResults.Ok(meta, "doti cycle precommit-guard", "Sanctioned commit in progress.");
        }

        return CliResults.Fail(meta, "doti cycle precommit-guard", ExitClass.Usage,
            [Diag.Of(ErrorCodes.Usage_InvalidArguments, global::Hx.Cycle.Core.PrecommitGuard.RedirectMessage)],
            "Bare git commit is redirected to the sanctioned path.",
            nextActions: [new CliNextAction("Use the sanctioned commit path", "Bare commits are blocked by the insurance hook.", "doti cycle commit --message \"…\"")]);
    }

    public static CliResult InstallHooks(CliMeta meta, string repo)
    {
        string hookPath = HookInstaller.Install(repo);
        return CliResults.Ok(meta, "doti install-hooks", "Installed the insurance pre-commit hook.",
            new { hookPath }, effects: [new CliEffect("write", hookPath, "insurance pre-commit hook")]);
    }

    // ---- doti question check (Layers B+C) ----

    public static CliResult QuestionCheck(CliMeta meta, string file)
    {
        if (string.IsNullOrWhiteSpace(file))
        {
            return Usage(meta, "doti question check", "--file is required.");
        }

        OperatorQuestion? question;
        try
        {
            question = JsonSerializer.Deserialize<OperatorQuestion>(File.ReadAllText(file), JsonOptions);
        }
        catch (Exception ex)
        {
            return CliResults.Fail(meta, "doti question check", ExitClass.Validation,
                [Diag.Of(ErrorCodes.Validation_Failed, $"Could not read/parse the question file: {ex.Message}", target: file)]);
        }

        if (question is null)
        {
            return CliResults.Fail(meta, "doti question check", ExitClass.Validation,
                [Diag.Of(ErrorCodes.Validation_Failed, "The file is empty or not a JSON object.", target: file)]);
        }

        OperatorQuestionValidation validation = OperatorQuestionValidator.Validate(question);
        if (validation.Valid)
        {
            return CliResults.Ok(meta, "doti question check", "The operator question is valid.", validation);
        }

        List<Diagnostic> errors = validation.Errors.Select(e => Diag.Of(ErrorCodes.Validation_Failed, e)).ToList();
        return CliResults.Fail(meta, "doti question check", ExitClass.Validation, errors,
            "The operator question violates the protocol.", validation);
    }

    // ---- architecture ----

    public static CliResult ArchitectureTest(CliMeta meta, string repo)
    {
        ArchitectureTestResult result = ArchitectureTestRunner.Run(repo);
        return CliResults.FromStage(meta, "architecture test", result.Outcome,
            $"{result.PassedCount}/{result.TestCount} passed; {result.Families.Count} families.", result);
    }

    // ---- gate run (Gate/Proof ring + NDJSON streaming) ----

    public static CliResult GateRun(CliMeta meta, string repo, string profile, Action<CliEvent> emit)
    {
        LaneDecision lane = LaneResolver.Resolve(profile);
        GateProof proof = lane.Outcome == StageOutcome.Fail
            ? new GateProof(JsonContractDefaults.SchemaVersion, StageOutcome.Fail,
                [new GateStep("lane", StageOutcome.Fail, [new GateEvidence("lane", lane.Reason)])], [])
            : GateRunner.Run(repo, lane.Lane, onStep: step => emit(new CliEvent(
                "step", step.Name, step.Outcome.ToString().ToLowerInvariant(), step.Evidence.FirstOrDefault()?.Message)));

        var runResult = new GateRunResult(JsonContractDefaults.SchemaVersion, lane, proof);

        string note = "";
        if (proof.Outcome == StageOutcome.Pass)
        {
            // Persist a change-set-bound proof so `doti cycle commit` can verify it is fresh + passing.
            try { GateProofStore.Persist(repo, lane.Lane, proof); }
            catch (Exception ex) { note = " (warning: proof not persisted: " + ex.Message + ")"; }
        }

        string summary = $"Gate {lane.Lane} ({lane.Reason}): {proof.Outcome}.{note}";
        return proof.Outcome == StageOutcome.Pass
            ? CliResults.Ok(meta, "gate run", summary, runResult)
            : CliResults.Fail(meta, "gate run", ExitClass.Validation,
                [Diag.Of(ErrorCodes.Validation_Failed, summary)], summary, runResult);
    }

    // ---- version ----

    public static CliResult VersionCalculate(CliMeta meta, string repo)
    {
        VersionResult result = GitVersionTool.Calculate(Path.GetFullPath(repo));
        return CliResults.Ok(meta, "version calculate", $"version={result.Version} ({result.Source}).", result);
    }

    public static CliResult VersionBump(CliMeta meta, string repo, bool major, bool minor)
    {
        if (major == minor)
        {
            return Usage(meta, "version bump", "Specify exactly one of --major or --minor.");
        }

        VersionResult result = GitVersionTool.Bump(Path.GetFullPath(repo), major ? "major" : "minor");
        return CliResults.Ok(meta, "version bump", $"version={result.Version} ({result.Source}).", result,
            effects: [new CliEffect("tag", result.Version, "annotated bump tag")]);
    }

    // ---- security ----

    public static CliResult SecurityScan(CliMeta meta, string repo)
    {
        SecurityScanResult result = SecurityScanner.Scan(Path.GetFullPath(repo));
        return CliResults.FromStage(meta, "security scan", result.Outcome,
            $"{result.Vulnerabilities.Count} vulnerability finding(s); SAST {result.SastStatus}.", result);
    }

    // ---- tools fetch (deterministic, hash-verified vendored-tool provisioning) ----

    public static CliResult ToolsFetch(CliMeta meta, string repo, string? rid, string toolFilter)
    {
        string hostRid = string.IsNullOrWhiteSpace(rid) ? Rid() : rid;
        string root = Path.GetFullPath(repo);

        IReadOnlyList<string> manifests = SelectToolManifests(toolFilter, out string? selectError);
        if (selectError is not null)
        {
            return Usage(meta, "tools fetch", selectError);
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("scaffold-dotnet-tool-fetch");
        byte[] FetchBytes(Uri url) => http.GetByteArrayAsync(url).GetAwaiter().GetResult();

        List<ToolFetchOutcome> outcomes = manifests
            .Select(relative => ToolFetcher.Fetch(
                RepositoryPathResolver.ResolveInside(root, relative).FullPath, hostRid, FetchBytes, root))
            .ToList();

        StageOutcome outcome =
            outcomes.Any(o => o.Status == ToolFetchStatus.Failed) ? StageOutcome.Fail :
            outcomes.All(o => o.Status == ToolFetchStatus.Fetched) ? StageOutcome.Pass :
            StageOutcome.Blocked;
        var result = new ToolFetchResult(JsonContractDefaults.SchemaVersion, outcome, hostRid, outcomes);

        int fetched = outcomes.Count(o => o.Status == ToolFetchStatus.Fetched);
        int skipped = outcomes.Count(o => o.Status == ToolFetchStatus.Skipped);
        int failed = outcomes.Count(o => o.Status == ToolFetchStatus.Failed);
        string summary = $"{fetched} fetched, {skipped} skipped (no asset for {hostRid}), {failed} failed.";

        if (outcome == StageOutcome.Fail)
        {
            // A failed fetch carries the failure kind's registered code (integrity on a hash mismatch,
            // validation/internal otherwise); the per-tool result stays in Data so an agent sees every outcome.
            List<Diagnostic> errors = outcomes
                .Where(o => o.Status == ToolFetchStatus.Failed)
                .Select(o => Diag.Of(FailureCode(o.FailureKind), o.Reason, target: o.Tool))
                .ToList();
            ExitClass exitClass = errors.Any(e => Diag.ExitClassOf(e.Code) == ExitClass.Integrity)
                ? ExitClass.Integrity
                : ExitClass.Validation;
            return CliResults.Fail(meta, "tools fetch", exitClass, errors, summary, result);
        }

        var effects = outcomes
            .Where(o => o.Status == ToolFetchStatus.Fetched && o.ExecutablePath is not null)
            .Select(o => new CliEffect("write", o.ExecutablePath!, $"{o.Tool} verified"))
            .ToList();
        return CliResults.Ok(meta, "tools fetch", summary, result, effects: effects);
    }

    private static IReadOnlyList<string> SelectToolManifests(string toolFilter, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(toolFilter) || string.Equals(toolFilter, "all", StringComparison.OrdinalIgnoreCase))
        {
            return ToolFetcher.ManifestRelativePaths;
        }

        string match = $"tools/{toolFilter.ToLowerInvariant()}/";
        List<string> selected = ToolFetcher.ManifestRelativePaths
            .Where(p => p.StartsWith(match, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (selected.Count == 0)
        {
            error = $"Unknown --tool '{toolFilter}'. Known: all, gitleaks, sentrux, gitversion.";
        }

        return selected;
    }

    private static string FailureCode(ToolFetchFailureKind kind) => kind switch
    {
        ToolFetchFailureKind.AssetUnavailable => ErrorCodes.Validation_ToolAssetUnavailable,
        ToolFetchFailureKind.ArchiveHashMismatch => ErrorCodes.Integrity_ToolArchiveHashMismatch,
        ToolFetchFailureKind.ExecutableHashMismatch => ErrorCodes.Integrity_ToolExecutableHashMismatch,
        _ => ErrorCodes.Internal_ToolDownloadFailed,
    };

    // ---- errorcodes (render + stability check) ----

    public static CliResult ErrorCodesRender(CliMeta meta, string repo)
    {
        string root = Path.GetFullPath(repo);
        IReadOnlyList<ErrorCodeEntry> entries = ErrorCodeRegistry.Load(Path.Combine(root, "errorcodes", "registry.json"));
        string generated = Path.Combine(root, "tools", "Hx.Cli.Kernel", "ErrorCodes.g.cs");
        File.WriteAllText(generated, ErrorCodeRegistry.RenderConstants(entries), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return CliResults.Ok(meta, "errorcodes render", $"Rendered {entries.Count} code(s) to ErrorCodes.g.cs.",
            new { codes = entries.Count, path = generated }, effects: [new CliEffect("write", generated, $"{entries.Count} codes")]);
    }

    public static CliResult ErrorCodesCheck(CliMeta meta, string repo)
    {
        string root = Path.GetFullPath(repo);
        IReadOnlyList<ErrorCodeEntry> current = ErrorCodeRegistry.Load(Path.Combine(root, "errorcodes", "registry.json"));
        IReadOnlyList<ShippedCode> shipped = ErrorCodeRegistry.LoadShipped(Path.Combine(root, "errorcodes", "shipped.json"));

        List<string> violations = [.. ErrorCodeRegistry.CheckStability(current, shipped)];
        if (!SameCodes(current, ErrorCodes.All))
        {
            violations.Add("ErrorCodes.g.cs is stale relative to registry.json; run `errorcodes render`.");
        }

        if (violations.Count == 0)
        {
            return CliResults.Ok(meta, "errorcodes check",
                $"{current.Count} code(s); {shipped.Count} shipped baseline intact.", new { codes = current.Count, shipped = shipped.Count });
        }

        List<Diagnostic> errors = violations.Select(v => Diag.Of(ErrorCodes.Integrity_VerificationFailed, v)).ToList();
        return CliResults.Fail(meta, "errorcodes check", ExitClass.Integrity, errors,
            "Error-code stability check failed.", new { violations });
    }

    private static bool SameCodes(IReadOnlyList<ErrorCodeEntry> a, IReadOnlyList<ErrorCodeEntry> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        for (int i = 0; i < a.Count; i++)
        {
            if (a[i].Code != b[i].Code || a[i].Name != b[i].Name || a[i].ExitClass != b[i].ExitClass
                || a[i].Number != b[i].Number || a[i].Severity != b[i].Severity)
            {
                return false;
            }
        }

        return true;
    }

    // ---- shared helpers ----

    private static CliResult Verify(CliMeta meta, string command, ToolVerificationResult result) =>
        CliResults.FromStage(meta, command, result.Outcome,
            result.Verified ? "Verified." : string.Join("; ", result.Problems), result, ExitClass.Integrity);

    private static CliResult Usage(CliMeta meta, string command, string message) =>
        CliResults.Fail(meta, command, ExitClass.Usage, [Diag.Of(ErrorCodes.Usage_InvalidArguments, message)]);

    private static string Rid() => HostPlatformDetector.DetectCurrent().RuntimeIdentifier;

    private static bool PrecommitGuard_IsSanctioned() => global::Hx.Cycle.Core.PrecommitGuard.IsSanctioned();

    private static bool TryParseAgents(string csv, out List<DotiAgentTarget> agents, out string? error)
    {
        agents = [];
        foreach (string key in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            DotiAgentTarget? agent = DotiAgentTarget.FromKey(key);
            if (agent is null)
            {
                error = $"Unknown agent '{key}'. Known: codex, claude.";
                return false;
            }

            agents.Add(agent);
        }

        if (agents.Count == 0)
        {
            agents.AddRange(DotiAgentTarget.All);
        }

        error = null;
        return true;
    }

    private static string? FindDotiSource(string start)
    {
        DirectoryInfo? dir = new(Path.GetFullPath(start));
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "doti", "core", "skills.json")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }
}
