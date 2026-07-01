using Hx.Doti.Core;
using Hx.Doti.Core.ManagedAssets;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Doti.Tests;

/// <summary>
/// 032 D2(e)/(f)/(g): the vendored-tool dirs (<c>tools/gitleaks</c>|<c>sentrux</c>|<c>gitversion</c>)
/// <see cref="ManagedAssets.ManagedAssetScanner.DotiSourcePaths"/> already scans into <c>managed-assets.json</c>
/// (category <c>doti-source</c>) but which, before this fix, NO consumer loop ever reconciled or checked — a stale
/// vendored-tool manifest (the ergon-stuck-on-Sentrux-v0.5.11 incident) was recorded-managed but silently never
/// updated/validated. These tests prove: (e) <see cref="DotiInstaller"/> now reconciles <c>tools/{sub}</c> with the
/// SAME preserve/<c>.new</c>/force machinery the <c>.doti</c> loop uses, never touching <c>bin/</c>; (f)
/// <see cref="DotiPayloadParityChecker"/>'s <c>tools/</c> coverage (exercised via the internal
/// <c>CheckToolFiles</c> seam, since the public <c>Check</c> entrypoint is a fresh-install SELF-consistency check
/// that, by construction, can never observe genuine drift — a copy from a source always reproduces it exactly); and
/// (g) <see cref="DotiUpdater"/> surfaces a <c>hx tools fetch --tool &lt;tool&gt;</c> advisory when a tools/{sub}
/// manifest changed, without ever auto-fetching the binary. All fixture-based file-IO — no network.
/// </summary>
public sealed class DotiVendoredToolReconcileTests
{
    // --- (e) DotiInstaller reconcile ---

    [Fact]
    public void Reconcile_brings_a_stale_vendored_tool_manifest_to_the_payloads_version_and_records_it()
    {
        string oldSource = DotiVersionTestSupport.NewSource("1.0.0", includeConstitution: true, includeTools: true);
        string newSource = DotiVersionTestSupport.NewSource("2.0.0");
        string target = DotiVersionTestSupport.NewTempDir();
        try
        {
            // Re-stamp the OLD source's sentrux manifest at v0.5.11 (the stale fixture) and seed the target from it.
            DotiVersionTestSupport.WriteVendoredTools(oldSource, sentruxReleaseTag: "v0.5.11");
            DotiInstaller.Install(oldSource, target, DotiAgentTarget.All, "t");
            string sentruxManifest = Path.Combine(target, "tools", "sentrux", "sentrux.version.json");
            Assert.Contains("v0.5.11", File.ReadAllText(sentruxManifest));

            // The NEW source carries the payload's v0.5.12 sentrux manifest.
            DotiVersionTestSupport.WriteVendoredTools(newSource, sentruxReleaseTag: "v0.5.12");

            DotiInstallResult result = DotiInstaller.Install(newSource, target, DotiAgentTarget.All, "t");

            Assert.Equal(StageOutcome.Pass, result.Outcome);
            Assert.Contains("v0.5.12", File.ReadAllText(sentruxManifest));
            Assert.DoesNotContain("v0.5.11", File.ReadAllText(sentruxManifest));

            // 032 D2(e): the reconcile records the touched tools/sentrux path in Installed (the "Changes" the CLI
            // surfaces) — the headline this whole defect is about: a vendored-tool change must be VISIBLE, never
            // silently absorbed.
            Assert.Contains(result.Installed, e => e.Path.Replace('\\', '/') == "tools/sentrux");

            // Rebaselined: the managed-assets.json baseline now records the NEW manifest's canonical hash (clean).
            ManagedAssetScanResult scan = ManagedAssetScanner.Scan(target);
            ManagedAssetStatus sentruxStatus = Assert.Single(
                scan.Assets, a => a.Path.Replace('\\', '/') == "tools/sentrux/sentrux.version.json");
            Assert.Equal(ManagedAssetState.Clean, sentruxStatus.State);
        }
        finally
        {
            DotiVersionTestSupport.ForceDelete(oldSource);
            DotiVersionTestSupport.ForceDelete(newSource);
            DotiVersionTestSupport.ForceDelete(target);
        }
    }

    [Fact]
    public void Operator_edited_vendored_tool_file_is_preserved_with_a_new_sidecar()
    {
        string source = DotiVersionTestSupport.NewSource("2.0.0", includeConstitution: true, includeTools: true);
        string target = DotiVersionTestSupport.NewTempDir();
        try
        {
            DotiInstaller.Install(source, target, DotiAgentTarget.All, "t");
            string sentruxManifest = Path.Combine(target, "tools", "sentrux", "sentrux.version.json");
            const string operatorEdit = """{ "schemaVersion": 1, "tool": "sentrux", "releaseTag": "v0.5.12", "operatorNote": "pinned locally" }""";
            File.WriteAllText(sentruxManifest, operatorEdit);

            DotiInstallResult result = DotiInstaller.Install(source, target, DotiAgentTarget.All, "t");

            // The operator's edit survives untouched; the bundled version lands beside it as a .new sidecar — the
            // SAME managed-replace-preserve-live-config machinery the .doti loop already uses (reused, not
            // reinvented), proven here for a tools/{sub} asset for the first time.
            Assert.Equal(operatorEdit, File.ReadAllText(sentruxManifest));
            Assert.True(File.Exists(sentruxManifest + ".new"), "the bundled manifest should be staged as a .new sidecar");
            Assert.Contains(result.Preserved, e => e.Path.Replace('\\', '/') == "tools/sentrux/sentrux.version.json");
            Assert.Contains(result.MergePending ?? [], e => e.Path.Replace('\\', '/') == "tools/sentrux/sentrux.version.json.new");
        }
        finally
        {
            DotiVersionTestSupport.ForceDelete(source);
            DotiVersionTestSupport.ForceDelete(target);
        }
    }

    [Fact]
    public void Grammar_files_get_the_same_reconcile_coverage_as_the_manifest()
    {
        string oldSource = DotiVersionTestSupport.NewSource("1.0.0", includeConstitution: true, includeTools: true);
        string target = DotiVersionTestSupport.NewTempDir();
        try
        {
            DotiInstaller.Install(oldSource, target, DotiAgentTarget.All, "t");
            string grammar = Path.Combine(target, "tools", "sentrux", "grammars", "csharp", "grammars", "windows-x86_64.dll");
            Assert.Equal("FAKE-GRAMMAR-BYTES", File.ReadAllText(grammar));

            // A new source with a genuinely different grammar payload.
            string newSource = DotiVersionTestSupport.NewSource("2.0.0");
            DotiVersionTestSupport.WriteVendoredTools(newSource, sentruxReleaseTag: "v0.5.12");
            File.WriteAllText(
                Path.Combine(newSource, "tools", "sentrux", "grammars", "csharp", "grammars", "windows-x86_64.dll"),
                "FAKE-GRAMMAR-BYTES-V2");
            try
            {
                DotiInstallResult result = DotiInstaller.Install(newSource, target, DotiAgentTarget.All, "t");

                Assert.Equal("FAKE-GRAMMAR-BYTES-V2", File.ReadAllText(grammar));
                Assert.Contains(result.Installed, e => e.Path.Replace('\\', '/') == "tools/sentrux");
            }
            finally
            {
                DotiVersionTestSupport.ForceDelete(newSource);
            }
        }
        finally
        {
            DotiVersionTestSupport.ForceDelete(oldSource);
            DotiVersionTestSupport.ForceDelete(target);
        }
    }

    [Fact]
    public void Vendored_tool_bin_directory_is_byte_unchanged_after_reconcile()
    {
        // Negative test: tools/{sub}/bin/ (the gitignored, per-RID, network-fetched executable) must NEVER be staged,
        // overwritten, or sidecar'd by the reconcile — it stays EXCLUSIVELY `hx tools fetch`'s concern. The reconcile
        // never POPULATES bin/ either (confirmed by the seed install below leaving it absent), so this models the
        // realistic case: an operator already ran `hx tools fetch` (the exe exists locally), and a LATER reconcile
        // must leave that fetched binary completely alone.
        string oldSource = DotiVersionTestSupport.NewSource("1.0.0", includeConstitution: true, includeTools: true);
        string newSource = DotiVersionTestSupport.NewSource("2.0.0");
        string target = DotiVersionTestSupport.NewTempDir();
        try
        {
            DotiInstaller.Install(oldSource, target, DotiAgentTarget.All, "t");
            string exePath = Path.Combine(target, "tools", "sentrux", "bin", "win-x64", "sentrux.exe");
            Assert.False(File.Exists(exePath), "the reconcile must never populate bin/ either — it stays untouched");

            // Simulate an operator-run `hx tools fetch`: the exe now exists locally.
            Directory.CreateDirectory(Path.GetDirectoryName(exePath)!);
            File.WriteAllText(exePath, "OPERATOR-FETCHED-EXE-BYTES");
            byte[] before = File.ReadAllBytes(exePath);

            // The new source's bin/ deliberately carries DIFFERENT bytes — if the reconcile ever touched bin/, this
            // would change; proving it does NOT is the point of the test.
            DotiVersionTestSupport.WriteVendoredTools(newSource, sentruxReleaseTag: "v0.5.12");
            File.WriteAllText(
                Path.Combine(newSource, "tools", "sentrux", "bin", "win-x64", "sentrux.exe"), "DIFFERENT-EXE-BYTES");

            DotiInstaller.Install(newSource, target, DotiAgentTarget.All, "t");

            byte[] after = File.ReadAllBytes(exePath);
            Assert.Equal(before, after);
            Assert.Equal("OPERATOR-FETCHED-EXE-BYTES", System.Text.Encoding.UTF8.GetString(after));
        }
        finally
        {
            DotiVersionTestSupport.ForceDelete(oldSource);
            DotiVersionTestSupport.ForceDelete(newSource);
            DotiVersionTestSupport.ForceDelete(target);
        }
    }

    // --- (f) DotiPayloadParityChecker coverage + drift detection ---

    [Fact]
    public void Payload_check_examines_vendored_tool_files_and_passes_when_consistent()
    {
        // The PUBLIC Check() entrypoint is a fresh-install self-consistency check: it installs FROM source INTO a
        // temp copy and compares them, so a fresh copy ALWAYS matches its own source by construction. This proves
        // the new tools/ coverage is WIRED IN (the vendored-tool files are examined and reported with the new
        // "vendored-tool" kind) for a realistic fixture — the positive case + the coverage claim.
        string source = DotiVersionTestSupport.NewSource("2.0.0", includeConstitution: true, includeTools: true);
        try
        {
            DotiPayloadCheckResult result = DotiPayloadParityChecker.Check(source);

            DotiPayloadFileStatus[] toolFiles = [.. result.Files.Where(f => f.Kind == "vendored-tool")];
            Assert.NotEmpty(toolFiles);
            Assert.All(toolFiles, f => Assert.True(f.Matches, $"{f.SourcePath} should match a self-consistent fresh install"));
            Assert.Contains(toolFiles, f => f.SourcePath == "tools/sentrux/sentrux.version.json");
            Assert.Contains(toolFiles, f => f.SourcePath == "tools/sentrux/grammars/csharp/grammars/windows-x86_64.dll");
            Assert.Contains(toolFiles, f => f.SourcePath == "tools/gitleaks/gitleaks.version.json");
            Assert.Contains(toolFiles, f => f.SourcePath == "tools/gitversion/gitversion.version.json");
            // bin/ must never be examined — the gitignored, network-fetched executable is out of scope.
            Assert.DoesNotContain(toolFiles, f => f.SourcePath.Contains("/bin/", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(StageOutcome.Pass, result.Outcome);
        }
        finally
        {
            DotiVersionTestSupport.ForceDelete(source);
        }
    }

    [Fact]
    public void A_stale_or_missing_vendored_tool_manifest_fails_the_comparison_with_kind_vendored_tool()
    {
        // Exercises CheckToolFiles directly (the internal seam — see the InternalsVisibleTo grant) against
        // hand-built source/target fixtures, since the public Check() wrapper's self-consistency design can never
        // observe genuine drift (a fresh copy from a source always matches it exactly). This proves the comparison
        // and drift-reporting LOGIC itself: a manifest present in source but STALE (different bytes) or MISSING in
        // target is reported as a non-matching "vendored-tool" entry — the gap that let ergon silently sit on
        // Sentrux v0.5.11.
        string source = DotiVersionTestSupport.NewTempDir();
        string target = DotiVersionTestSupport.NewTempDir();
        try
        {
            string sourceManifest = Path.Combine(source, "tools", "sentrux", "sentrux.version.json");
            Directory.CreateDirectory(Path.GetDirectoryName(sourceManifest)!);
            File.WriteAllText(sourceManifest, """{ "schemaVersion": 1, "tool": "sentrux", "releaseTag": "v0.5.12" }""");

            string sourceGrammar = Path.Combine(source, "tools", "sentrux", "grammars", "csharp", "grammars", "windows-x86_64.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(sourceGrammar)!);
            File.WriteAllText(sourceGrammar, "GRAMMAR-V2");

            // target's manifest is STALE (a different byte sequence — the v0.5.11 fixture).
            string targetManifest = Path.Combine(target, "tools", "sentrux", "sentrux.version.json");
            Directory.CreateDirectory(Path.GetDirectoryName(targetManifest)!);
            File.WriteAllText(targetManifest, """{ "schemaVersion": 1, "tool": "sentrux", "releaseTag": "v0.5.11" }""");
            // target's grammar is entirely MISSING.

            DotiPayloadFileStatus[] statuses = [.. DotiPayloadParityChecker.CheckToolFiles(source, target)];

            DotiPayloadFileStatus manifestStatus = Assert.Single(
                statuses, s => s.SourcePath == "tools/sentrux/sentrux.version.json");
            Assert.False(manifestStatus.Matches);
            Assert.Equal("vendored-tool", manifestStatus.Kind);
            Assert.NotNull(manifestStatus.ExpectedSha256);
            Assert.NotNull(manifestStatus.ActualSha256);
            Assert.NotEqual(manifestStatus.ExpectedSha256, manifestStatus.ActualSha256);

            DotiPayloadFileStatus grammarStatus = Assert.Single(
                statuses, s => s.SourcePath == "tools/sentrux/grammars/csharp/grammars/windows-x86_64.dll");
            Assert.False(grammarStatus.Matches);
            Assert.Equal("vendored-tool", grammarStatus.Kind);
            Assert.Equal("installed file missing", grammarStatus.Reason);
        }
        finally
        {
            DotiVersionTestSupport.ForceDelete(source);
            DotiVersionTestSupport.ForceDelete(target);
        }
    }

    [Fact]
    public void CheckToolFiles_never_examines_bin_even_when_present_and_stale()
    {
        string source = DotiVersionTestSupport.NewTempDir();
        string target = DotiVersionTestSupport.NewTempDir();
        try
        {
            string sourceExe = Path.Combine(source, "tools", "sentrux", "bin", "win-x64", "sentrux.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(sourceExe)!);
            File.WriteAllText(sourceExe, "SOURCE-EXE-V2");

            string targetExe = Path.Combine(target, "tools", "sentrux", "bin", "win-x64", "sentrux.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(targetExe)!);
            File.WriteAllText(targetExe, "TARGET-EXE-STALE"); // deliberately different — would fail if examined.

            DotiPayloadFileStatus[] statuses = [.. DotiPayloadParityChecker.CheckToolFiles(source, target)];

            Assert.Empty(statuses); // bin/ is the ONLY file present on either side — nothing should be reported.
        }
        finally
        {
            DotiVersionTestSupport.ForceDelete(source);
            DotiVersionTestSupport.ForceDelete(target);
        }
    }

    // --- (g) DotiUpdater advisory ---

    [Fact]
    public void Update_advises_running_tools_fetch_when_a_vendored_tool_manifest_changed()
    {
        string oldSource = DotiVersionTestSupport.NewSource("1.0.0", includeConstitution: true, includeTools: true);
        string newSource = DotiVersionTestSupport.NewSource("2.0.0");
        string target = DotiVersionTestSupport.NewTempDir();
        try
        {
            // Re-stamp oldSource at the STALE v0.5.11 (NewSource's includeTools default is v0.5.12 — match the
            // pattern used by the other "stale manifest" test so the seed install genuinely differs from newSource).
            DotiVersionTestSupport.WriteVendoredTools(oldSource, sentruxReleaseTag: "v0.5.11");
            DotiInstaller.Install(oldSource, target, DotiAgentTarget.All, "t");
            DotiVersionTestSupport.WriteVendoredTools(newSource, sentruxReleaseTag: "v0.5.12");

            DotiUpdateOutcome outcome = DotiUpdater.Update(newSource, target, DotiAgentTarget.All, "2.0.0", force: false);

            Assert.NotNull(outcome.Reason);
            Assert.Contains("hx tools fetch --tool sentrux", outcome.Reason!);
            Assert.Contains("vendored tool manifest updated", outcome.Reason!, StringComparison.OrdinalIgnoreCase);
            // The exe itself is NEVER auto-fetched — update/install must stay offline/deterministic; the manifest
            // moving forward never causes bin/ to be populated or touched.
            string exePath = Path.Combine(target, "tools", "sentrux", "bin", "win-x64", "sentrux.exe");
            Assert.False(File.Exists(exePath), "the manifest advisory must never trigger an auto-fetch of the binary");
        }
        finally
        {
            DotiVersionTestSupport.ForceDelete(oldSource);
            DotiVersionTestSupport.ForceDelete(newSource);
            DotiVersionTestSupport.ForceDelete(target);
        }
    }

    [Fact]
    public void Update_does_not_advise_when_no_vendored_tool_manifest_changed()
    {
        string source = DotiVersionTestSupport.NewSource("2.0.0", includeConstitution: true, includeTools: true);
        string target = DotiVersionTestSupport.NewTempDir();
        try
        {
            DotiInstaller.Install(source, target, DotiAgentTarget.All, "t");

            // A second, idempotent update from the SAME source — nothing in tools/ should have changed.
            DotiUpdateOutcome outcome = DotiUpdater.Update(source, target, DotiAgentTarget.All, "2.0.0", force: false);

            Assert.Equal(DotiUpdateStatus.AlreadyCurrent, outcome.Status);
            if (outcome.Reason is not null)
            {
                Assert.DoesNotContain("tools fetch", outcome.Reason, StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            DotiVersionTestSupport.ForceDelete(source);
            DotiVersionTestSupport.ForceDelete(target);
        }
    }
}
