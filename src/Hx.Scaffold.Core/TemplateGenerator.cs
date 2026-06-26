using Hx.Tooling.Contracts;

namespace Hx.Scaffold.Core;

/// <summary>
/// Generates the base solution by invoking the template via subprocess <c>dotnet new</c>
/// (subprocess first). Packs the in-repo template pack into a sandboxed
/// <c>DOTNET_CLI_HOME</c> (never touching the real machine's templates), installs it, instantiates,
/// and uninstalls. Records a <see cref="TemplateInvocation"/> for the proof.
/// </summary>
public static class TemplateGenerator
{
    public const string TemplateShortName = "hx-dotnet-cli";
    public const string PackId = "Hx.Scaffold.Templates";
    public const string PackProjectRelative = "scaffold/Hx.Scaffold.Templates.csproj";

    public static TemplateInvocation Generate(ScaffoldRequest request, string sourceRepoRoot)
    {
        var symbols = new Dictionary<string, string>
        {
            ["name"] = request.Name,
            ["company"] = request.Company,
            ["output"] = request.OutputPath,
        };

        string sandbox = Path.Combine(Path.GetTempPath(), "hx-scaffold-new-" + Guid.NewGuid().ToString("n"));
        string cliHome = Path.Combine(sandbox, "clihome");
        string pkgOut = Path.Combine(sandbox, "pkg");
        Directory.CreateDirectory(cliHome);

        Dictionary<string, string> env = ProcessRunner.NestedDotnetEnv();
        env["DOTNET_CLI_HOME"] = cliHome; // sandbox the template install root

        try
        {
            string? nupkg = ResolveTemplateNupkg(sourceRepoRoot, env, pkgOut, symbols, out TemplateInvocation? packFailure);
            if (nupkg is null)
            {
                return packFailure!;
            }

            (int code, string output) install = ProcessRunner.Run(
                "dotnet", $"new install \"{nupkg}\"", sourceRepoRoot, env);
            if (install.code != 0)
            {
                return Fail(symbols, "install failed: " + ProcessRunner.Tail(install.output));
            }

            try
            {
                (int code, string output) create = ProcessRunner.Run(
                    "dotnet",
                    $"new {TemplateShortName} -n {request.Name} -o \"{request.OutputPath}\" --company \"{request.Company}\"",
                    sourceRepoRoot, env);
                if (create.code != 0)
                {
                    return Fail(symbols, "instantiate failed: " + ProcessRunner.Tail(create.output));
                }

                return new TemplateInvocation(TemplateShortName, PackProjectRelative, symbols, StageOutcome.Pass);
            }
            finally
            {
                ProcessRunner.Run("dotnet", $"new uninstall {PackId}", sourceRepoRoot, env);
            }
        }
        finally
        {
            TryDelete(sandbox);
        }
    }

    // 007 T021: prefer a pre-built template pack bundled in the installed payload (source-free; T023 bundles it
    // beside the executable). In dev/self-host the asset root resolves to the source tree (FR-004), so no bundled
    // pack exists and we `dotnet pack` the in-repo template project — the source-mode fallback.
    private static string? ResolveTemplateNupkg(
        string sourceRepoRoot,
        IReadOnlyDictionary<string, string> env,
        string pkgOut,
        IReadOnlyDictionary<string, string> symbols,
        out TemplateInvocation? failure)
    {
        failure = null;
        string assetRoot = InstalledPayload.ResolveAssetRoot(sourceRepoRoot);
        // 007 T023 stages the bundled template pack under `<payloadRoot>/templates/` (AddHxPayloadToPackage), so the
        // installed (source-free) tool MUST look there — searching only the payload-root top dir misses it and falls
        // to the dev-only `dotnet pack`, which fails with no source repo (the SC-001 regression T047's smoke caught).
        // The top-dir is kept as a defensive secondary in case a future layout co-locates the pack with the manifest.
        string? bundled = FindBundledPack(Path.Combine(assetRoot, "templates")) ?? FindBundledPack(assetRoot);
        if (bundled is not null)
        {
            return bundled;
        }

        string packProject = Path.Combine(sourceRepoRoot, PackProjectRelative.Replace('/', Path.DirectorySeparatorChar));
        (int code, string output) pack = ProcessRunner.Run(
            "dotnet", $"pack \"{packProject}\" -c Release -o \"{pkgOut}\" --nologo --disable-build-servers",
            sourceRepoRoot, env);
        if (pack.code != 0)
        {
            failure = Fail(symbols, "pack failed: " + ProcessRunner.Tail(pack.output));
            return null;
        }

        return Directory.GetFiles(pkgOut, "*.nupkg").Single();
    }

    /// <summary>The newest bundled template pack (<c>Hx.Scaffold.Templates*.nupkg</c>) in <paramref name="dir"/>,
    /// or null if the directory is absent or holds none. <c>internal</c> so the source-free resolution is unit-tested
    /// without the heavy install smoke (007 T047).</summary>
    internal static string? FindBundledPack(string dir) =>
        Directory.Exists(dir)
            ? Directory.EnumerateFiles(dir, PackId + "*.nupkg", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.Ordinal)
                .LastOrDefault()
            : null;

    private static TemplateInvocation Fail(IReadOnlyDictionary<string, string> symbols, string message) =>
        new(TemplateShortName, message, symbols, StageOutcome.Fail);

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // best-effort sandbox cleanup
        }
    }
}
