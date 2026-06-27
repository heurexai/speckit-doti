namespace Hx.Doti.Core;

public enum ConstitutionInitOutcome
{
    /// <summary>No constitution existed; one was written from the template with the title filled.</summary>
    Initialized,

    /// <summary>A constitution already existed; the operator's content was preserved untouched.</summary>
    Preserved,
}

public sealed record ConstitutionInitResult(ConstitutionInitOutcome Outcome, string Path, string? ProjectName);

/// <summary>
/// Initializes a repo's <c>.doti/memory/constitution.md</c> from the structured §1/§2 template (009 FR-010), filling
/// the <c>{PROJECT_NAME}</c> title token (FR-015). It is a managed-asset-preservation writer: it writes ONLY when no
/// constitution exists, so a re-run of <c>doti install</c> never resurrects the template over an operator-edited
/// constitution (SC-006). Idempotent. The caller supplies the template content (read from the installed template) so
/// this stays a pure, testable unit.
/// </summary>
public static class ConstitutionInitializer
{
    public const string TitleToken = "{PROJECT_NAME}";

    public static ConstitutionInitResult Initialize(string repositoryRoot, string templateContent, string projectName)
    {
        string target = Path.Combine(repositoryRoot, ".doti", "memory", "constitution.md");
        if (File.Exists(target))
        {
            // Operator-owned once it exists — never overwrite (FR-010 preservation / SC-006).
            return new ConstitutionInitResult(ConstitutionInitOutcome.Preserved, ConstitutionService.RelativePath, null);
        }

        string filled = templateContent.Replace(TitleToken, projectName);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, filled);
        return new ConstitutionInitResult(ConstitutionInitOutcome.Initialized, ConstitutionService.RelativePath, projectName);
    }
}
