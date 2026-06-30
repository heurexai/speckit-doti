namespace Hx.Doti.Core;

/// <summary>
/// 031 T001 (FR-001, D1): resolve the SOURCE the global-tool's <c>doti install</c>/<c>update</c>/<c>update-all</c>
/// reconcile from. The installed .NET global tool ships its version-stamped <c>.doti</c> payload BESIDE the
/// executable, so the bundled payload root is <see cref="AppContext.BaseDirectory"/> — the same root whose
/// <c>…/.doti/core/prerequisites.json</c> is <c>hx version</c>'s <c>manifestPath</c>. This is the direct, in-process
/// truth (no <c>hx version</c> JSON parse, no process hop). Returns that root when its <c>.doti/core/skills.json</c>
/// exists (the payload is present), else null so the CLI falls through to the working-directory dev walk
/// (<c>FindDotiSource</c>) — a <c>dotnet run</c> from source has a bin <see cref="AppContext.BaseDirectory"/> with no
/// <c>.doti</c>, so it correctly returns null there.
/// </summary>
public static class BundledPayloadResolver
{
    /// <summary>The bundled payload root beside the executable, or null when none ships there (e.g. a source build).</summary>
    public static string? Resolve() => Resolve(AppContext.BaseDirectory);

    /// <summary>Testable core: <paramref name="baseDirectory"/> is the bundled payload root iff its
    /// <c>.doti/core/skills.json</c> exists.</summary>
    public static string? Resolve(string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            return null;
        }

        string skills = Path.Combine(baseDirectory, ".doti", "core", "skills.json");
        return File.Exists(skills) ? baseDirectory : null;
    }
}
