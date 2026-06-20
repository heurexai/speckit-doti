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

        string packProject = Path.Combine(sourceRepoRoot, PackProjectRelative.Replace('/', Path.DirectorySeparatorChar));

        try
        {
            (int code, string output) pack = ProcessRunner.Run(
                "dotnet", $"pack \"{packProject}\" -c Release -o \"{pkgOut}\" --nologo --disable-build-servers",
                sourceRepoRoot, env);
            if (pack.code != 0)
            {
                return Fail(symbols, "pack failed: " + ProcessRunner.Tail(pack.output));
            }

            string nupkg = Directory.GetFiles(pkgOut, "*.nupkg").Single();

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
