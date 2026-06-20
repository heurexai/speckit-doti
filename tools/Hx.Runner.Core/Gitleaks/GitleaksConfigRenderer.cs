using System.Text;
using Hx.Runner.Core.Hygiene;

namespace Hx.Runner.Core.Gitleaks;

/// <summary>
/// Renders the native Gitleaks config (<c>tools/gitleaks/config/gitleaks.toml</c>)
/// from the stable <c>rules/hygiene.json</c> policy. Uses <c>useDefault = true</c>
/// and appends scaffold allowlist entries rather than forking the upstream rules.
/// Line endings are always LF so the rendered bytes — and therefore the pinned
/// config hash in <c>gitleaks.version.json</c> — are identical on every platform.
/// </summary>
public static class GitleaksConfigRenderer
{
    private const char Lf = '\n';

    public static string Render(HygienePolicy policy)
    {
        var builder = new StringBuilder();
        Line(builder, "# Generated from rules/hygiene.json by Hx.Runner.Cli.");
        Line(builder, "# Do not edit by hand; edit rules/hygiene.json and re-render.");
        Line(builder, "title = \"scaffold-dotnet hygiene\"");
        Line(builder, "");
        Line(builder, "[extend]");
        Line(builder, "useDefault = true");
        Line(builder, "");
        Line(builder, "[allowlist]");
        Line(builder, "description = \"scaffold-dotnet allowlist rendered from rules/hygiene.json\"");

        if (policy.ExcludePaths.Count > 0)
        {
            Line(builder, "paths = [");
            foreach (string path in policy.ExcludePaths)
            {
                Line(builder, $"  '''{EscapePathRegex(path)}''',");
            }

            Line(builder, "]");
        }

        return builder.ToString();
    }

    private static void Line(StringBuilder builder, string text) => builder.Append(text).Append(Lf);

    private static string EscapePathRegex(string path)
    {
        // Treat policy excludes as path prefixes anchored at the repo-relative root.
        string normalized = path.Replace('\\', '/').TrimEnd('/');
        return "^" + System.Text.RegularExpressions.Regex.Escape(normalized);
    }
}
