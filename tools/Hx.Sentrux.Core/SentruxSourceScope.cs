namespace Hx.Sentrux.Core;

/// <summary>
/// The Sentrux SOURCE scope (FR-029/SC-016): which repo-relative paths Sentrux measures. The floor is the
/// <c>.sentruxignore</c> exclusion of prose (docs, <c>*.md</c>, <c>.doti/</c>, <c>.agents/</c>, <c>.claude/</c>) —
/// Sentrux gauges CODE complexity/architecture, not prose; the override is the policy's <c>CodeExtensions</c> plus an
/// explicit "configured as code" list (a prose path is out of scope UNLESS explicitly configured as code). Mirrors
/// the <c>.sentruxignore</c> the native tool reads, so the C# scope and the tool scope agree.
/// </summary>
public static class SentruxSourceScope
{
    public static bool IsInScope(string path, SentruxPolicy policy)
    {
        string normalized = path.Replace('\\', '/');
        if (policy.EffectiveConfiguredAsCode.Any(configured => Matches(normalized, configured)))
        {
            return true; // explicitly configured as code (FR-029 override)
        }

        if (IsExcludedProse(normalized))
        {
            return false; // the .sentruxignore prose floor (SC-016)
        }

        return policy.EffectiveCodeExtensions.Any(ext => normalized.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsExcludedProse(string path) =>
        path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("docs/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(".doti/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(".agents/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(".claude/", StringComparison.OrdinalIgnoreCase);

    private static bool Matches(string path, string configured)
    {
        string normalized = configured.Replace('\\', '/').TrimEnd('/');
        return normalized.Length > 0
            && (path.Equals(normalized, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(normalized + "/", StringComparison.OrdinalIgnoreCase));
    }
}
