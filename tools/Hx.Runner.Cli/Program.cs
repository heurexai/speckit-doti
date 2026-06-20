using System.CommandLine;
using System.Reflection;
using Hx.Cli.Kernel;
using Hx.Runner.Cli;

CliMeta meta = new("hx-runner", CliApp.ResolveVersion(Assembly.GetExecutingAssembly()));

RootCommand rootCommand = new("scaffold-dotnet deterministic runner");

// ---- bootstrap-proof ----
Command proofCommand = new("bootstrap-proof", "Emit the bootstrap advisory proof.");
Option<bool> proofJson = CliApp.JsonOption();
proofCommand.Options.Add(proofJson);
proofCommand.SetAction(parseResult => CliHost.Run(meta, "bootstrap-proof",
    () => RunnerCommands.BootstrapProof(meta), forceJson: CliApp.ForceJson(parseResult, proofJson)));
rootCommand.Subcommands.Add(proofCommand);

// ---- platform ----
Command platformCommand = new("platform", "Cross-platform diagnostics.");
Command platformProbeCommand = new("probe", "Emit platform warning-mode diagnostics.");
Option<bool> probeJson = CliApp.JsonOption();
platformProbeCommand.Options.Add(probeJson);
platformProbeCommand.SetAction(parseResult => CliHost.Run(meta, "platform probe",
    () => RunnerCommands.PlatformProbe(meta), forceJson: CliApp.ForceJson(parseResult, probeJson)));
platformCommand.Subcommands.Add(platformProbeCommand);
rootCommand.Subcommands.Add(platformCommand);

// ---- hygiene ----
Command hygieneCommand = new("hygiene", "Public-release hygiene scanning.");

Command hygieneScanCommand = new("scan", "Run a public hygiene scan (changed-file by default).");
Option<string> scanRepo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
Option<string> scanScope = new("--scope") { Description = "changed | all", DefaultValueFactory = _ => "changed" };
Option<string> scanSource = new("--source") { Description = "staged | range", DefaultValueFactory = _ => "staged" };
Option<string?> scanBase = new("--base") { Description = "Base ref for a range scan." };
Option<string?> scanHead = new("--head") { Description = "Head ref for a range scan." };
Option<bool> scanJson = CliApp.JsonOption();
hygieneScanCommand.Options.Add(scanRepo);
hygieneScanCommand.Options.Add(scanScope);
hygieneScanCommand.Options.Add(scanSource);
hygieneScanCommand.Options.Add(scanBase);
hygieneScanCommand.Options.Add(scanHead);
hygieneScanCommand.Options.Add(scanJson);
hygieneScanCommand.SetAction(parseResult => CliHost.Run(meta, "hygiene scan",
    () => RunnerCommands.HygieneScan(meta, parseResult.GetValue(scanRepo)!, parseResult.GetValue(scanScope)!,
        parseResult.GetValue(scanSource)!, parseResult.GetValue(scanBase), parseResult.GetValue(scanHead)),
    forceJson: CliApp.ForceJson(parseResult, scanJson)));
hygieneCommand.Subcommands.Add(hygieneScanCommand);

Command gitleaksCommand = new("gitleaks", "Gitleaks tool localization checks.");

Command gitleaksVerifyCommand = new("verify", "Verify the vendored Gitleaks manifest, executable, license, and config.");
Option<string> glVerifyRepo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
Option<bool> glVerifyJson = CliApp.JsonOption();
gitleaksVerifyCommand.Options.Add(glVerifyRepo);
gitleaksVerifyCommand.Options.Add(glVerifyJson);
gitleaksVerifyCommand.SetAction(parseResult => CliHost.Run(meta, "hygiene gitleaks verify",
    () => RunnerCommands.GitleaksVerify(meta, parseResult.GetValue(glVerifyRepo)!),
    forceJson: CliApp.ForceJson(parseResult, glVerifyJson)));
gitleaksCommand.Subcommands.Add(gitleaksVerifyCommand);

Command gitleaksUpdateCommand = new("update-check", "Check the pinned Gitleaks release against upstream (explicit, network-enabled).");
Option<string> glUpdateRepo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
Option<bool> glUpdateJson = CliApp.JsonOption();
gitleaksUpdateCommand.Options.Add(glUpdateRepo);
gitleaksUpdateCommand.Options.Add(glUpdateJson);
gitleaksUpdateCommand.SetAction(parseResult => CliHost.Run(meta, "hygiene gitleaks update-check",
    () => RunnerCommands.GitleaksUpdateCheck(meta, parseResult.GetValue(glUpdateRepo)!),
    forceJson: CliApp.ForceJson(parseResult, glUpdateJson)));
gitleaksCommand.Subcommands.Add(gitleaksUpdateCommand);

Command gitleaksRenderCommand = new("render-config", "Render tools/gitleaks/config/gitleaks.toml deterministically from rules/hygiene.json.");
Option<string> glRenderRepo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
Option<bool> glRenderJson = CliApp.JsonOption();
gitleaksRenderCommand.Options.Add(glRenderRepo);
gitleaksRenderCommand.Options.Add(glRenderJson);
gitleaksRenderCommand.SetAction(parseResult => CliHost.Run(meta, "hygiene gitleaks render-config",
    () => RunnerCommands.GitleaksRenderConfig(meta, parseResult.GetValue(glRenderRepo)!),
    forceJson: CliApp.ForceJson(parseResult, glRenderJson)));
gitleaksCommand.Subcommands.Add(gitleaksRenderCommand);

hygieneCommand.Subcommands.Add(gitleaksCommand);
rootCommand.Subcommands.Add(hygieneCommand);

// ---- sentrux ----
Command sentruxCommand = new("sentrux", "Sentrux structural-quality gate.");

Command sentruxVerifyCommand = new("verify", "Verify the vendored Sentrux manifest, binary, grammars, and fork stamp.");
Option<string> sxVerifyRepo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
Option<bool> sxVerifyJson = CliApp.JsonOption();
sentruxVerifyCommand.Options.Add(sxVerifyRepo);
sentruxVerifyCommand.Options.Add(sxVerifyJson);
sentruxVerifyCommand.SetAction(parseResult => CliHost.Run(meta, "sentrux verify",
    () => RunnerCommands.SentruxVerify(meta, parseResult.GetValue(sxVerifyRepo)!),
    forceJson: CliApp.ForceJson(parseResult, sxVerifyJson)));
sentruxCommand.Subcommands.Add(sentruxVerifyCommand);

Command sentruxBaselineCommand = new("baseline", "Create the Sentrux baseline (first smoke / explicit operator action).");
Option<string> sxBaseRepo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
Option<bool> sxBaseJson = CliApp.JsonOption();
sentruxBaselineCommand.Options.Add(sxBaseRepo);
sentruxBaselineCommand.Options.Add(sxBaseJson);
sentruxBaselineCommand.SetAction(parseResult => CliHost.Run(meta, "sentrux baseline",
    () => RunnerCommands.SentruxBaseline(meta, parseResult.GetValue(sxBaseRepo)!),
    forceJson: CliApp.ForceJson(parseResult, sxBaseJson)));
sentruxCommand.Subcommands.Add(sentruxBaselineCommand);

Command sentruxCheckCommand = new("check", "Run the merged Sentrux rule check + regression gate.");
Option<string> sxCheckRepo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
Option<bool> sxCheckJson = CliApp.JsonOption();
sentruxCheckCommand.Options.Add(sxCheckRepo);
sentruxCheckCommand.Options.Add(sxCheckJson);
sentruxCheckCommand.SetAction(parseResult => CliHost.Run(meta, "sentrux check",
    () => RunnerCommands.SentruxCheck(meta, parseResult.GetValue(sxCheckRepo)!),
    forceJson: CliApp.ForceJson(parseResult, sxCheckJson)));
sentruxCommand.Subcommands.Add(sentruxCheckCommand);

rootCommand.Subcommands.Add(sentruxCommand);

// ---- doti (self-hosting: skill rendering + drift, cycle state, hooks, question protocol) ----
Command dotiCommand = new("doti", "Doti self-hosting: render skills, cycle state, hooks, operator-question protocol.");

Command renderSkillsCommand = new("render-skills",
    "Render installed Codex/Claude skills + shared agent context from one source; --check reports drift (fail closed).");
Option<string> rsRepo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
Option<string> rsAgents = new("--agents") { Description = "Comma-separated agents (codex,claude).", DefaultValueFactory = _ => "codex,claude" };
Option<bool> rsCheck = new("--check") { Description = "Check for drift instead of writing.", DefaultValueFactory = _ => false };
Option<bool> rsJson = CliApp.JsonOption();
renderSkillsCommand.Options.Add(rsRepo);
renderSkillsCommand.Options.Add(rsAgents);
renderSkillsCommand.Options.Add(rsCheck);
renderSkillsCommand.Options.Add(rsJson);
renderSkillsCommand.SetAction(parseResult => CliHost.Run(meta, "doti render-skills",
    () => RunnerCommands.DotiRenderSkills(meta, parseResult.GetValue(rsRepo)!, parseResult.GetValue(rsAgents)!, parseResult.GetValue(rsCheck)),
    forceJson: CliApp.ForceJson(parseResult, rsJson)));
dotiCommand.Subcommands.Add(renderSkillsCommand);

Command installCommand = new("install", "Install Doti assets (doti/ source + rendered skills + repo metadata) into a target repo.");
Option<string> diRepo = new("--repo") { Description = "Target repository root to install into.", DefaultValueFactory = _ => "." };
Option<string> diAgents = new("--agents") { Description = "Comma-separated agents (codex,claude).", DefaultValueFactory = _ => "codex,claude" };
Option<bool> diJson = CliApp.JsonOption();
installCommand.Options.Add(diRepo);
installCommand.Options.Add(diAgents);
installCommand.Options.Add(diJson);
installCommand.SetAction(parseResult => CliHost.Run(meta, "doti install",
    () => RunnerCommands.DotiInstall(meta, parseResult.GetValue(diRepo)!, parseResult.GetValue(diAgents)!),
    forceJson: CliApp.ForceJson(parseResult, diJson)));
dotiCommand.Subcommands.Add(installCommand);

// ---- doti cycle ----
Command cycleCommand = new("cycle", "Doti cycle state: stamp diff-bound stage proofs, report freshness, and enforce the chokepoints.");

Command cycleStampCommand = new("stamp", "Record a stage's diff-bound proof and advance the cycle state.");
Option<string> csStage = new("--stage") { Description = "Stage id (specify, clarify, plan, tasks, analyze, arch-review, implement, drift-review, commit, release).", DefaultValueFactory = _ => "" };
Option<string> csFeature = new("--feature") { Description = "Feature slug (required on the first stamp; e.g. phase-14-doti-cycle-state).", DefaultValueFactory = _ => "" };
Option<string> csBase = new("--base") { Description = "Base ref for the change-set identity (default: dev if it resolves, else HEAD).", DefaultValueFactory = _ => "" };
Option<string> csRepo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
Option<bool> csJson = CliApp.JsonOption();
cycleStampCommand.Options.Add(csStage);
cycleStampCommand.Options.Add(csFeature);
cycleStampCommand.Options.Add(csBase);
cycleStampCommand.Options.Add(csRepo);
cycleStampCommand.Options.Add(csJson);
cycleStampCommand.SetAction(parseResult => CliHost.Run(meta, "doti cycle stamp",
    () => RunnerCommands.CycleStamp(meta, parseResult.GetValue(csRepo)!, parseResult.GetValue(csStage)!,
        parseResult.GetValue(csFeature)!, parseResult.GetValue(csBase)!),
    forceJson: CliApp.ForceJson(parseResult, csJson)));
cycleCommand.Subcommands.Add(cycleStampCommand);

Command cycleStatusCommand = new("status", "Report the cycle state with a freshness verdict per stamped stage (non-enforcing).");
Option<string> cstRepo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
Option<bool> cstJson = CliApp.JsonOption();
cycleStatusCommand.Options.Add(cstRepo);
cycleStatusCommand.Options.Add(cstJson);
cycleStatusCommand.SetAction(parseResult => CliHost.Run(meta, "doti cycle status",
    () => RunnerCommands.CycleStatus(meta, parseResult.GetValue(cstRepo)!),
    forceJson: CliApp.ForceJson(parseResult, cstJson)));
cycleCommand.Subcommands.Add(cycleStatusCommand);

Command cycleCheckCommand = new("check", "Fail-closed: verify a stage's prerequisites are all stamped + fresh + valid.");
Option<string> ccStage = new("--stage") { Description = "Stage id whose prerequisites to verify.", DefaultValueFactory = _ => "" };
Option<string> ccRepo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
Option<bool> ccJson = CliApp.JsonOption();
cycleCheckCommand.Options.Add(ccStage);
cycleCheckCommand.Options.Add(ccRepo);
cycleCheckCommand.Options.Add(ccJson);
cycleCheckCommand.SetAction(parseResult => CliHost.Run(meta, "doti cycle check",
    () => RunnerCommands.CycleCheck(meta, parseResult.GetValue(ccRepo)!, parseResult.GetValue(ccStage)!),
    forceJson: CliApp.ForceJson(parseResult, ccJson)));
cycleCommand.Subcommands.Add(cycleCheckCommand);

Command cycleCommitCommand = new("commit", "The sanctioned commit path: verify prerequisites + a fresh passing gate proof + a clean staged scope, then commit; refuse otherwise.");
Option<string> cmRepo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
Option<string> cmMessage = new("--message") { Description = "The commit message (the operator authors intent).", DefaultValueFactory = _ => "" };
Option<bool> cmJson = CliApp.JsonOption();
cycleCommitCommand.Options.Add(cmRepo);
cycleCommitCommand.Options.Add(cmMessage);
cycleCommitCommand.Options.Add(cmJson);
cycleCommitCommand.SetAction(parseResult => CliHost.Run(meta, "doti cycle commit",
    () => RunnerCommands.CycleCommit(meta, parseResult.GetValue(cmRepo)!, parseResult.GetValue(cmMessage)!),
    forceJson: CliApp.ForceJson(parseResult, cmJson)));
cycleCommand.Subcommands.Add(cycleCommitCommand);

Command precommitGuardCommand = new("precommit-guard", "Insurance-hook guard: exit 0 if the sanctioned-commit sentinel is set, else redirect to `doti cycle commit`.");
Option<bool> pgJson = CliApp.JsonOption();
precommitGuardCommand.Options.Add(pgJson);
precommitGuardCommand.SetAction(parseResult => CliHost.Run(meta, "doti cycle precommit-guard",
    () => RunnerCommands.PrecommitGuard(meta), forceJson: CliApp.ForceJson(parseResult, pgJson)));
cycleCommand.Subcommands.Add(precommitGuardCommand);

dotiCommand.Subcommands.Add(cycleCommand);

// ---- doti install-hooks ----
Command installHooksCommand = new("install-hooks", "Install the doti insurance pre-commit hook into this repo's (untracked) git hooks dir.");
Option<string> ihRepo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
Option<bool> ihJson = CliApp.JsonOption();
installHooksCommand.Options.Add(ihRepo);
installHooksCommand.Options.Add(ihJson);
installHooksCommand.SetAction(parseResult => CliHost.Run(meta, "doti install-hooks",
    () => RunnerCommands.InstallHooks(meta, parseResult.GetValue(ihRepo)!),
    forceJson: CliApp.ForceJson(parseResult, ihJson)));
dotiCommand.Subcommands.Add(installHooksCommand);

// ---- doti question check ----
Command questionCommand = new("question", "Operator-question protocol tooling.");
Command questionCheckCommand = new("check", "Fail-closed: validate an OperatorQuestion JSON file against the protocol.");
Option<string> qcFile = new("--file") { Description = "Path to the OperatorQuestion JSON file.", DefaultValueFactory = _ => "" };
Option<bool> qcJson = CliApp.JsonOption();
questionCheckCommand.Options.Add(qcFile);
questionCheckCommand.Options.Add(qcJson);
questionCheckCommand.SetAction(parseResult => CliHost.Run(meta, "doti question check",
    () => RunnerCommands.QuestionCheck(meta, parseResult.GetValue(qcFile)!),
    forceJson: CliApp.ForceJson(parseResult, qcJson)));
questionCommand.Subcommands.Add(questionCheckCommand);
dotiCommand.Subcommands.Add(questionCommand);

rootCommand.Subcommands.Add(dotiCommand);

// ---- architecture ----
Command architectureCommand = new("architecture", "Architecture gate: run the rule families.");

Command archTestCommand = new("test", "Run the repo's ArchUnitNET architecture families and emit a per-test proof.");
Option<string> atRepo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
Option<bool> atJson = CliApp.JsonOption();
archTestCommand.Options.Add(atRepo);
archTestCommand.Options.Add(atJson);
archTestCommand.SetAction(parseResult => CliHost.Run(meta, "architecture test",
    () => RunnerCommands.ArchitectureTest(meta, parseResult.GetValue(atRepo)!),
    forceJson: CliApp.ForceJson(parseResult, atJson)));
architectureCommand.Subcommands.Add(archTestCommand);

rootCommand.Subcommands.Add(architectureCommand);

// ---- gate (aggregate the deterministic ladder into one fail-closed proof; NDJSON --stream) ----
Command gateCommand = new("gate", "Run the deterministic gate ladder and emit one aggregated proof.");
Command gateRunCommand = new("run", "Run the gate ladder for a lane and emit a fail-closed GateProof.");
Option<string> grRepo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
Option<string> grProfile = new("--profile") { Description = "Lane profile: auto|advisory|normal|release.", DefaultValueFactory = _ => "auto" };
Option<bool> grStream = new("--stream") { Description = "Stream NDJSON phase events as the ladder runs (JSON sink only).", DefaultValueFactory = _ => false };
Option<bool> grJson = CliApp.JsonOption();
gateRunCommand.Options.Add(grRepo);
gateRunCommand.Options.Add(grProfile);
gateRunCommand.Options.Add(grStream);
gateRunCommand.Options.Add(grJson);
gateRunCommand.SetAction(parseResult => CliHost.RunStreaming(meta, "gate run",
    emit => RunnerCommands.GateRun(meta, parseResult.GetValue(grRepo)!, parseResult.GetValue(grProfile)!, emit),
    forceJson: CliApp.ForceJson(parseResult, grJson),
    streamEvents: parseResult.GetValue(grStream)));
gateCommand.Subcommands.Add(gateRunCommand);
rootCommand.Subcommands.Add(gateCommand);

// ---- version ----
Command versionCommand = new("version", "GitVersion-backed version calculation and operator-instructed bumps.");

Command versionCalculateCommand = new("calculate", "Compute the version via the vendored GitVersion CLI (fail closed if unvendored).");
Option<string> vcRepo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
Option<bool> vcJson = CliApp.JsonOption();
versionCalculateCommand.Options.Add(vcRepo);
versionCalculateCommand.Options.Add(vcJson);
versionCalculateCommand.SetAction(parseResult => CliHost.Run(meta, "version calculate",
    () => RunnerCommands.VersionCalculate(meta, parseResult.GetValue(vcRepo)!),
    forceJson: CliApp.ForceJson(parseResult, vcJson)));
versionCommand.Subcommands.Add(versionCalculateCommand);

Command versionBumpCommand = new("bump", "Record an operator-instructed major/minor bump as an annotated git tag (the sole bump surface).");
Option<string> vbRepo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
Option<bool> vbMajor = new("--major") { Description = "Major bump.", DefaultValueFactory = _ => false };
Option<bool> vbMinor = new("--minor") { Description = "Minor bump.", DefaultValueFactory = _ => false };
Option<bool> vbJson = CliApp.JsonOption();
versionBumpCommand.Options.Add(vbRepo);
versionBumpCommand.Options.Add(vbMajor);
versionBumpCommand.Options.Add(vbMinor);
versionBumpCommand.Options.Add(vbJson);
versionBumpCommand.SetAction(parseResult => CliHost.Run(meta, "version bump",
    () => RunnerCommands.VersionBump(meta, parseResult.GetValue(vbRepo)!, parseResult.GetValue(vbMajor), parseResult.GetValue(vbMinor)),
    forceJson: CliApp.ForceJson(parseResult, vbJson)));
versionCommand.Subcommands.Add(versionBumpCommand);
rootCommand.Subcommands.Add(versionCommand);

// ---- security ----
Command securityCommand = new("security", "Security gate: package-vulnerability scan + analyzer-enforced SAST status.");
Command securityScanCommand = new("scan", "Run the package-vulnerability SCA + report SAST enforcement; fail closed on findings >= the policy floor.");
Option<string> secRepo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
Option<bool> secJson = CliApp.JsonOption();
securityScanCommand.Options.Add(secRepo);
securityScanCommand.Options.Add(secJson);
securityScanCommand.SetAction(parseResult => CliHost.Run(meta, "security scan",
    () => RunnerCommands.SecurityScan(meta, parseResult.GetValue(secRepo)!),
    forceJson: CliApp.ForceJson(parseResult, secJson)));
securityCommand.Subcommands.Add(securityScanCommand);
rootCommand.Subcommands.Add(securityCommand);

// ---- errorcodes (render from registry.json; check the append-only shipped freeze) ----
Command errorcodesCommand = new("errorcodes", "Error-code registry: render the generated constants and check stability.");

Command errorcodesRenderCommand = new("render", "Regenerate tools/Hx.Cli.Kernel/ErrorCodes.g.cs from errorcodes/registry.json.");
Option<string> ecRenderRepo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
Option<bool> ecRenderJson = CliApp.JsonOption();
errorcodesRenderCommand.Options.Add(ecRenderRepo);
errorcodesRenderCommand.Options.Add(ecRenderJson);
errorcodesRenderCommand.SetAction(parseResult => CliHost.Run(meta, "errorcodes render",
    () => RunnerCommands.ErrorCodesRender(meta, parseResult.GetValue(ecRenderRepo)!),
    forceJson: CliApp.ForceJson(parseResult, ecRenderJson)));
errorcodesCommand.Subcommands.Add(errorcodesRenderCommand);

Command errorcodesCheckCommand = new("check", "Fail-closed: every shipped code is still registered, unchanged, and the generated file is current.");
Option<string> ecCheckRepo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
Option<bool> ecCheckJson = CliApp.JsonOption();
errorcodesCheckCommand.Options.Add(ecCheckRepo);
errorcodesCheckCommand.Options.Add(ecCheckJson);
errorcodesCheckCommand.SetAction(parseResult => CliHost.Run(meta, "errorcodes check",
    () => RunnerCommands.ErrorCodesCheck(meta, parseResult.GetValue(ecCheckRepo)!),
    forceJson: CliApp.ForceJson(parseResult, ecCheckJson)));
errorcodesCommand.Subcommands.Add(errorcodesCheckCommand);
rootCommand.Subcommands.Add(errorcodesCommand);

// ---- describe (kernel-generated capability model) ----
CliApp.AddDescribe(rootCommand, meta, ErrorCodes.All);

return rootCommand.Parse(args).Invoke();
