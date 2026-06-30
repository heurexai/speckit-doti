using Hx.Tooling.Contracts.Setup;

namespace Hx.Doti.Core.Setup;

/// <summary>
/// 029 FR-006/D3/D9: projects the operator's constitution §2 prose verbatim into
/// <c>.doti/memory/constitution.md</c>. WRITE-ONCE-WHEN-PLACEHOLDER: a §2 sub-section is filled ONLY while it still
/// holds its unfilled bracket placeholder (<c>[DOMAIN_PRINCIPLES]</c> …); an already-authored §2 sub-section is
/// preserved (never clobbered, mirroring <see cref="ConstitutionInitializer"/>). ANCHOR-INTEGRITY (D9): a projected
/// value that forges a <c>## §1</c>/<c>## §2</c> heading is refused, so <see cref="ConstitutionService.ExtractSection2"/>
/// can never be broken by config input. Idempotent — re-running over a filled section is a no-op.
/// </summary>
public sealed class ConstitutionSection2Writer : ISetupTargetWriter
{
    private static readonly IReadOnlyDictionary<string, string> PlaceholderByKey = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [SetupKeys.ConstitutionDomainPrinciples] = "[DOMAIN_PRINCIPLES]",
        [SetupKeys.ConstitutionTechStack] = "[TECH_STACK]",
        [SetupKeys.ConstitutionCodingStyle] = "[CODING_STYLE]",
        [SetupKeys.ConstitutionSecurityCompliance] = "[SECURITY_COMPLIANCE]",
        [SetupKeys.ConstitutionPerformance] = "[PERFORMANCE]",
    };

    public SetupTarget Target => SetupTarget.ConstitutionSection2;

    public IReadOnlyList<string> Write(string repositoryRoot, IReadOnlyList<ResolvedSetupField> fields)
    {
        string path = SetupAssetPaths.ResolveInside(repositoryRoot, ConstitutionService.RelativePath);
        if (!File.Exists(path))
        {
            return [];
        }

        string content = File.ReadAllText(path);
        string updated = content;
        foreach (ResolvedSetupField field in fields)
        {
            if (!PlaceholderByKey.TryGetValue(field.Key, out string? placeholder))
            {
                continue;
            }

            string value = field.Field.Value;
            RejectForgedAnchor(field.Key, value);

            // Write-once: only replace the still-present placeholder; an operator-filled section keeps its content.
            if (updated.Contains(placeholder, StringComparison.Ordinal))
            {
                updated = updated.Replace(placeholder, value);
            }
        }

        if (string.Equals(updated, content, StringComparison.Ordinal))
        {
            return []; // nothing to fill (all sections already authored) — idempotent.
        }

        File.WriteAllText(path, updated);
        return [ConstitutionService.RelativePath];
    }

    /// <summary>D9 anchor integrity: a §2 value MUST NOT introduce a forged §1/§2 heading that would break the
    /// verbatim §2 slice. A line that (trimmed) starts with <c>## §1</c> or <c>## §2</c> is refused.</summary>
    private static void RejectForgedAnchor(string key, string value)
    {
        foreach (string line in value.Split('\n'))
        {
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("## §1", StringComparison.Ordinal)
                || trimmed.StartsWith(ConstitutionService.Section2Anchor, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Refusing to project constitution value for '{key}': it forges a '## §1'/'## §2' heading (anchor integrity).");
            }
        }
    }
}
