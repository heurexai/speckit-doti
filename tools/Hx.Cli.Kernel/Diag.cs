using Hx.Tooling.Contracts;

namespace Hx.Cli.Kernel;

/// <summary>
/// Builds a <see cref="Diagnostic"/> from a registered error code. The code carries severity, exit class, message,
/// and remediation from the registry, so callers never restate them (and cannot invent a code — an unregistered
/// code throws, which the build/tests surface).
/// </summary>
public static class Diag
{
    private static readonly Dictionary<string, ErrorCodeEntry> ByCode =
        ErrorCodes.All.ToDictionary(e => e.Code, StringComparer.Ordinal);

    /// <summary>The registry entry for a code; throws if the code is not registered.</summary>
    public static ErrorCodeEntry Entry(string code) =>
        ByCode.TryGetValue(code, out ErrorCodeEntry? entry)
            ? entry
            : throw new ArgumentException(
                $"Unregistered error code '{code}' — add it to errorcodes/registry.json and re-render.", nameof(code));

    /// <summary>Build a diagnostic for <paramref name="code"/>; optionally override the message and pin a target/location.</summary>
    public static Diagnostic Of(string code, string? message = null, string? target = null, CliLocation? location = null)
    {
        ErrorCodeEntry e = Entry(code);
        return new Diagnostic(e.Code, e.Severity, message ?? e.Message, target, location, e.Remediation, e.Blocking);
    }

    /// <summary>The <see cref="ExitClass"/> a code maps to (so the host can resolve the process exit code).</summary>
    public static ExitClass ExitClassOf(string code) => Entry(code).ExitClass;
}
