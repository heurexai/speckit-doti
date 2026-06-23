using System.Text;
using Hx.Cli.Kernel;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Cli;

public static partial class RunnerCommands
{
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
}
