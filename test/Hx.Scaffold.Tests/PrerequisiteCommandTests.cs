using System.Text.Json;
using Hx.Cli.Kernel;
using Hx.Runner.Core.Process;
using Hx.Scaffold.Cli;
using Hx.Scaffold.Core;
using Hx.Scaffold.Core.Prerequisites;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Scaffold.Tests;

public sealed partial class ScaffoldCommandsTests
{
    [Fact]
    public void Prereq_check_reports_missing_dotnet_with_trusted_winget_plan()
    {
        string parent = NewTempDir("hx-prereq-new-");
        try
        {
            CliResult result = ScaffoldCommands.PrereqCheck(
                Meta,
                "new",
                ".",
                Path.Combine(parent, "Demo.App"),
                FakePrerequisites(dotnetInstalled: false, gitInstalled: true));

            Assert.False(result.Ok);
            Assert.Equal(ErrorCodes.Validation_PrerequisiteMissing, Assert.Single(result.Errors).Code);
            PrerequisiteCheckReport report = result.Data!.Deserialize<PrerequisiteCheckReport>(
                JsonContractSerializerOptions.Create())!;
            Assert.Contains(report.Items, i => i.Id == "dotnet-sdk" && i.Status == "missing");
            Assert.NotNull(report.InstallPlan);
            Assert.Contains(report.InstallPlan!.Items, i => i.PackageId == "Microsoft.DotNet.SDK.10");
        }
        finally
        {
            ForceDelete(parent);
        }
    }

    [Fact]
    public void Prereq_install_requires_exact_plan_confirmation()
    {
        string parent = NewTempDir("hx-prereq-install-");
        try
        {
            CliResult result = ScaffoldCommands.PrereqInstall(
                Meta,
                "new",
                ".",
                Path.Combine(parent, "Demo.App"),
                "",
                FakePrerequisites(dotnetInstalled: false, gitInstalled: true));

            Assert.False(result.Ok);
            Assert.True(result.RequiresOperator);
            Assert.Contains(result.Errors, e => e.Code == ErrorCodes.Validation_PrerequisiteInstallNotApproved);
            PrerequisiteCheckReport report = result.Data!.Deserialize<PrerequisiteCheckReport>(
                JsonContractSerializerOptions.Create())!;
            Assert.NotNull(report.InstallPlan);
        }
        finally
        {
            ForceDelete(parent);
        }
    }

    [Fact]
    public void Prereq_install_executes_winget_only_after_confirmed_plan_and_rechecks()
    {
        string parent = NewTempDir("hx-prereq-confirm-");
        bool dotnetInstalled = false;
        int wingetInstalls = 0;
        PrerequisiteServices services = FakePrerequisites(
            () => dotnetInstalled,
            gitInstalled: true,
            onWingetInstall: () =>
            {
                wingetInstalls++;
                dotnetInstalled = true;
            });
        try
        {
            string output = Path.Combine(parent, "Demo.App");
            string sourceRoot = ScaffoldRoot.Resolve(Directory.GetCurrentDirectory());
            PrerequisiteCheckReport pending = PrerequisitePreflight.Check(
                new PrerequisiteCheckRequest(sourceRoot, PrerequisiteCommands.New, OutputPath: output),
                services);

            CliResult result = ScaffoldCommands.PrereqInstall(
                Meta,
                "new",
                ".",
                output,
                pending.InstallPlan!.Digest,
                services);

            Assert.True(result.Ok);
            Assert.Equal(1, wingetInstalls);
            PrerequisiteCheckReport report = result.Data!.Deserialize<PrerequisiteCheckReport>(
                JsonContractSerializerOptions.Create())!;
            Assert.Contains(report.InstallExecutions, e => e.PackageId == "Microsoft.DotNet.SDK.10");
            Assert.Contains(report.Items, i => i.Id == "dotnet-sdk" && i.Status == "found");
        }
        finally
        {
            ForceDelete(parent);
        }
    }

    [Fact]
    public void Prereq_check_rejects_unknown_target_command()
    {
        CliResult result = ScaffoldCommands.PrereqCheck(
            Meta,
            "bogus",
            ".",
            "",
            FakePrerequisites(dotnetInstalled: true, gitInstalled: true));

        Assert.False(result.Ok);
        Assert.Equal((int)ExitClass.Usage, result.ExitCode);
        Assert.Equal(ErrorCodes.Usage_InvalidArguments, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void Prereq_install_plan_digest_changes_when_manifest_identity_changes()
    {
        string sourceRoot = NewTempDir("hx-prereq-manifest-");
        try
        {
            string manifestDir = Path.Combine(sourceRoot, "doti", "core");
            Directory.CreateDirectory(manifestDir);
            string manifestPath = Path.Combine(manifestDir, "prerequisites.json");
            string original = File.ReadAllText(Path.Combine(
                ScaffoldRoot.Resolve(Directory.GetCurrentDirectory()),
                "doti",
                "core",
                "prerequisites.json"));
            File.WriteAllText(manifestPath, original);

            PrerequisiteServices services = FakePrerequisites(dotnetInstalled: false, gitInstalled: true);
            PrerequisiteCheckReport first = PrerequisitePreflight.Check(
                new PrerequisiteCheckRequest(sourceRoot, PrerequisiteCommands.New, OutputPath: sourceRoot),
                services);

            File.WriteAllText(manifestPath, original.Replace(
                "official .NET download page",
                "official .NET SDK download page",
                StringComparison.Ordinal));

            PrerequisiteCheckReport second = PrerequisitePreflight.Check(
                new PrerequisiteCheckRequest(sourceRoot, PrerequisiteCommands.New, OutputPath: sourceRoot),
                services);

            Assert.NotEqual(first.ManifestSha256, second.ManifestSha256);
            Assert.NotEqual(first.InstallPlan!.Digest, second.InstallPlan!.Digest);
        }
        finally
        {
            ForceDelete(sourceRoot);
        }
    }

    private static PrerequisiteServices FakePrerequisites(bool dotnetInstalled, bool gitInstalled) =>
        FakePrerequisites(() => dotnetInstalled, gitInstalled);

    private static PrerequisiteServices FakePrerequisites(
        Func<bool> dotnetInstalled,
        bool gitInstalled,
        Action? onWingetInstall = null) =>
        new()
        {
            IsWindows = () => true,
            RunProcess = (file, args, _) =>
            {
                if (file == "dotnet")
                {
                    return dotnetInstalled()
                        ? new ProcessRunResult(0, "10.0.301 [C:\\dotnet\\sdk]\n", "")
                        : new ProcessRunResult(1, "", "dotnet was not found");
                }

                if (file == "git")
                {
                    return gitInstalled
                        ? new ProcessRunResult(0, "git version 2.54.0.windows.1\n", "")
                        : new ProcessRunResult(1, "", "git was not found");
                }

                if (file == "winget" && args.SequenceEqual(["--version"]))
                {
                    return new ProcessRunResult(0, "v1.10.0\n", "");
                }

                if (file == "winget" && args.Count > 0 && args[0] == "install")
                {
                    onWingetInstall?.Invoke();
                    return new ProcessRunResult(0, "installed\n", "");
                }

                return new ProcessRunResult(1, "", "unexpected process: " + file);
            }
        };
}
