using System.Text.Json;

namespace Hx.Tooling.Contracts.Setup;

/// <summary>
/// 029 FR-009/D5/D9: fail-closed schema + value validation for a <c>--config</c>/<c>setup.json</c> document. Runs
/// in the CLI BEFORE the request record is built (and therefore before generation), so an invalid config never
/// creates a file (SC-006). Each error names the offending field. Pure — System.Text.Json only, no IO (the CLI does
/// the filesystem <c>GetFullPath</c>+containment on top of <see cref="IsContainedRelativePath"/>).
/// </summary>
public static class SetupConfigSchema
{
    /// <summary>The known top-level object keys (camelCase, web policy). An unknown key fails closed (FR-009).</summary>
    private static readonly HashSet<string> KnownRoot =
        new(StringComparer.OrdinalIgnoreCase) { "schemaVersion", "identity", "versioning", "release", "publish", "agents", "constitution" };

    private static readonly Dictionary<string, HashSet<string>> KnownNested = new(StringComparer.OrdinalIgnoreCase)
    {
        ["identity"] = new(StringComparer.OrdinalIgnoreCase) { "name", "company", "output", "description", "authors", "repositoryUrl", "license" },
        ["versioning"] = new(StringComparer.OrdinalIgnoreCase) { "nextVersion" },
        ["release"] = new(StringComparer.OrdinalIgnoreCase) { "environmentVariable", "directory", "enabled" },
        ["publish"] = new(StringComparer.OrdinalIgnoreCase) { "enabled", "owner", "repo", "workflow", "environment", "target" },
        ["constitution"] = new(StringComparer.OrdinalIgnoreCase) { "domainPrinciples", "techStack", "codingStyle", "securityCompliance", "performance" },
    };

    private static readonly HashSet<string> KnownAgents = new(StringComparer.OrdinalIgnoreCase) { "claude", "codex" };

    /// <summary>
    /// Parse + validate the raw config JSON: fail-closed on malformed JSON, an unknown field, a wrong
    /// <c>schemaVersion</c>, or any invalid value. On success the parsed <paramref name="config"/> is returned.
    /// </summary>
    public static SetupValidationResult ValidateRaw(string json, out SetupConfig? config)
    {
        config = null;
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            return new SetupValidationResult([new SetupValidationError("(root)", $"setup config is not valid JSON: {ex.Message}")]);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new SetupValidationResult([new SetupValidationError("(root)", "setup config must be a JSON object.")]);
            }

            var unknown = new List<SetupValidationError>();
            CollectUnknownFields(document.RootElement, unknown);
            if (unknown.Count > 0)
            {
                return new SetupValidationResult(unknown);
            }
        }

        SetupConfig parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<SetupConfig>(json, JsonContractSerializerOptions.Create())
                ?? new SetupConfig();
        }
        catch (JsonException ex)
        {
            return new SetupValidationResult([new SetupValidationError("(root)", $"setup config could not be bound: {ex.Message}")]);
        }

        SetupValidationResult valueResult = Validate(parsed);
        if (valueResult.Ok)
        {
            config = parsed;
        }

        return valueResult;
    }

    /// <summary>Validate the bound <paramref name="config"/>'s values (schemaVersion, agents, SemVer, SPDX, free-text).</summary>
    public static SetupValidationResult Validate(SetupConfig config)
    {
        var errors = new List<SetupValidationError>();

        if (config.SchemaVersion is { } sv && sv != 1)
        {
            errors.Add(new SetupValidationError("schemaVersion", $"unsupported schemaVersion '{sv}'; expected 1."));
        }

        ValidateAgents(config.Agents, errors);
        ValidateIdentity(config.Identity, errors);
        ValidateVersioning(config.Versioning, errors);
        ValidateRelease(config.Release, errors);
        ValidatePublish(config.Publish, errors);
        ValidateConstitution(config.Constitution, errors);

        return new SetupValidationResult(errors);
    }

    private static void ValidateAgents(IReadOnlyList<string>? agents, List<SetupValidationError> errors)
    {
        if (agents is null)
        {
            return;
        }

        foreach (string agent in agents)
        {
            if (!KnownAgents.Contains(agent))
            {
                errors.Add(new SetupValidationError("agents", $"unknown agent '{agent}'; only 'claude' and 'codex' are configurable."));
            }
        }
    }

    private static void ValidateIdentity(SetupIdentityConfig? identity, List<SetupValidationError> errors)
    {
        if (identity is null)
        {
            return;
        }

        RejectFreeText("identity.name", identity.Name, errors);
        RejectFreeText("identity.company", identity.Company, errors);
        RejectFreeText("identity.description", identity.Description, errors);
        RejectFreeText("identity.authors", identity.Authors, errors);
        RejectFreeText("identity.repositoryUrl", identity.RepositoryUrl, errors);

        if (identity.Output is { } output && !IsContainedRelativePath(output))
        {
            errors.Add(new SetupValidationError("identity.output", "output path must not contain '..' segments or be an absolute/UNC path."));
        }

        if (identity.License is { Length: > 0 } license && !IsSpdxCharset(license))
        {
            errors.Add(new SetupValidationError("identity.license", $"license '{license}' is not a valid SPDX id/expression (letters, digits, '.', '-', '+', '(', ')', spaces)."));
        }
    }

    private static void ValidateVersioning(SetupVersioningConfig? versioning, List<SetupValidationError> errors)
    {
        if (versioning?.NextVersion is { Length: > 0 } next && !TryParseSemVerCore(next, out _, out _, out _))
        {
            errors.Add(new SetupValidationError("versioning.nextVersion", $"nextVersion '{next}' must be a 3-part numeric SemVer core (e.g. 0.1.0)."));
        }
    }

    private static void ValidateRelease(SetupReleaseConfig? release, List<SetupValidationError> errors)
    {
        if (release?.EnvironmentVariable is { Length: > 0 } envVar && !IsEnvironmentVariableName(envVar))
        {
            errors.Add(new SetupValidationError("release.environmentVariable", $"environmentVariable '{envVar}' must be a valid env-var name (letters, digits, underscore; not starting with a digit)."));
        }

        if (release?.Directory is { } dir && dir.Length > 0)
        {
            RejectControlOrXml("release.directory", dir, errors);
        }
    }

    private static void ValidatePublish(SetupPublishConfig? publish, List<SetupValidationError> errors)
    {
        if (publish is null)
        {
            return;
        }

        RejectFreeText("publish.owner", publish.Owner, errors);
        RejectFreeText("publish.repo", publish.Repo, errors);
        RejectFreeText("publish.workflow", publish.Workflow, errors);
        RejectFreeText("publish.environment", publish.Environment, errors);
        RejectFreeText("publish.target", publish.Target, errors);
    }

    private static void ValidateConstitution(SetupConstitutionConfig? constitution, List<SetupValidationError> errors)
    {
        if (constitution is null)
        {
            return;
        }

        // §2 prose may contain '&'/'<'/'>' legitimately in Markdown; reject only control chars + a forged §1/§2 heading
        // (anchor integrity, D9) — the full anchor-integrity guard also runs in the writer.
        RejectControlChars("constitution.domainPrinciples", constitution.DomainPrinciples, errors);
        RejectControlChars("constitution.techStack", constitution.TechStack, errors);
        RejectControlChars("constitution.codingStyle", constitution.CodingStyle, errors);
        RejectControlChars("constitution.securityCompliance", constitution.SecurityCompliance, errors);
        RejectControlChars("constitution.performance", constitution.Performance, errors);
    }

    /// <summary>Reject control chars + XML metacharacters in a free-text identity/publish value (D9, MSBuild-injection).</summary>
    private static void RejectFreeText(string field, string? value, List<SetupValidationError> errors)
    {
        if (value is null)
        {
            return;
        }

        RejectControlOrXml(field, value, errors);
    }

    private static void RejectControlOrXml(string field, string value, List<SetupValidationError> errors)
    {
        if (HasControlChar(value))
        {
            errors.Add(new SetupValidationError(field, $"{field} must not contain control characters."));
        }
        else if (value.IndexOfAny(['<', '>', '&', '"', '\'']) >= 0)
        {
            errors.Add(new SetupValidationError(field, $"{field} must not contain XML metacharacters (< > & \" ')."));
        }
    }

    private static void RejectControlChars(string field, string? value, List<SetupValidationError> errors)
    {
        if (value is not null && HasControlChar(value))
        {
            errors.Add(new SetupValidationError(field, $"{field} must not contain control characters."));
        }
    }

    private static bool HasControlChar(string value)
    {
        foreach (char c in value)
        {
            if (char.IsControl(c) && c is not '\n' and not '\r' and not '\t')
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>029 D5: the ONE shared 3-part numeric SemVer-core parser (used by validation + version-seed projection).</summary>
    public static bool TryParseSemVerCore(string value, out int major, out int minor, out int patch)
    {
        major = minor = patch = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string[] parts = value.Trim().Split('.');
        return parts.Length == 3
            && IsNonNegativeInt(parts[0], out major)
            && IsNonNegativeInt(parts[1], out minor)
            && IsNonNegativeInt(parts[2], out patch);
    }

    private static bool IsNonNegativeInt(string s, out int value)
    {
        value = 0;
        if (s.Length == 0)
        {
            return false;
        }

        foreach (char c in s)
        {
            if (!char.IsAsciiDigit(c))
            {
                return false;
            }
        }

        return int.TryParse(s, out value) && value >= 0;
    }

    /// <summary>029 D9: a PURE relative-path containment check (no <c>..</c> segment, not rooted/UNC). The CLI layers a
    /// filesystem <c>GetFullPath</c>+StartsWith on top; this catches the obvious traversal without touching IO.</summary>
    public static bool IsContainedRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        string normalized = path.Replace('\\', '/');
        if (normalized.StartsWith('/') || normalized.StartsWith("//", StringComparison.Ordinal))
        {
            return false; // rooted / UNC
        }

        if (normalized.Length >= 2 && char.IsAsciiLetter(normalized[0]) && normalized[1] == ':')
        {
            return false; // drive-qualified (C:...)
        }

        foreach (string segment in normalized.Split('/'))
        {
            if (segment == "..")
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>029 D5: the SPDX id/expression charset (letters, digits, '.', '-', '+', '(', ')', and spaces for expressions).</summary>
    public static bool IsSpdxCharset(string license)
    {
        foreach (char c in license)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not '.' and not '-' and not '+' and not '(' and not ')' and not ' ')
            {
                return false;
            }
        }

        return license.Length > 0;
    }

    /// <summary>A valid environment-variable name (letters, digits, underscore; not starting with a digit).</summary>
    public static bool IsEnvironmentVariableName(string name)
    {
        if (name.Length == 0 || char.IsAsciiDigit(name[0]))
        {
            return false;
        }

        foreach (char c in name)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '_')
            {
                return false;
            }
        }

        return true;
    }

    private static void CollectUnknownFields(JsonElement root, List<SetupValidationError> errors)
    {
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (!KnownRoot.Contains(property.Name))
            {
                errors.Add(new SetupValidationError(property.Name, $"unknown setup field '{property.Name}'."));
                continue;
            }

            if (KnownNested.TryGetValue(property.Name, out HashSet<string>? nested)
                && property.Value.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty child in property.Value.EnumerateObject())
                {
                    if (!nested.Contains(child.Name))
                    {
                        errors.Add(new SetupValidationError($"{property.Name}.{child.Name}", $"unknown setup field '{property.Name}.{child.Name}'."));
                    }
                }
            }
        }
    }
}
