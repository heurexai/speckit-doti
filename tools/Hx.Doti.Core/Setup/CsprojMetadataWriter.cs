using System.Xml.Linq;
using Hx.Tooling.Contracts.Setup;

namespace Hx.Doti.Core.Setup;

/// <summary>
/// 029 FR-006/D3/D9: projects the operator's identity metadata (<c>&lt;Description&gt;</c>,
/// <c>&lt;RepositoryUrl&gt;</c>, <c>&lt;PackageLicenseExpression&gt;</c>, <c>&lt;Authors&gt;</c>) into the generated
/// CLI <c>.csproj</c>. Every value is XML-ENCODED by writing through <see cref="XDocument"/> (so the framework escapes
/// <c>&amp;</c>/<c>&lt;</c> — a <c>description="A &amp; B"</c> yields a VALID .csproj, never MSBuild-injection). The
/// CLI project path is <c>src/{name}.Cli/{name}.Cli.csproj</c> (the same convention the release writer uses).
/// Idempotent: re-running over the produced repo overwrites the same elements with the same values.
/// </summary>
public sealed class CsprojMetadataWriter : ISetupTargetWriter
{
    private readonly string _projectName;

    public CsprojMetadataWriter(string projectName) => _projectName = projectName;

    public SetupTarget Target => SetupTarget.CsprojMetadata;

    public IReadOnlyList<string> Write(string repositoryRoot, IReadOnlyList<ResolvedSetupField> fields)
    {
        string relative = $"src/{_projectName}.Cli/{_projectName}.Cli.csproj";
        string path = SetupAssetPaths.ResolveInside(repositoryRoot, relative);
        if (!File.Exists(path))
        {
            return [];
        }

        XDocument document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
        XElement? propertyGroup = document.Root?.Elements("PropertyGroup").FirstOrDefault();
        if (propertyGroup is null)
        {
            return [];
        }

        bool changed = false;
        foreach (ResolvedSetupField field in fields)
        {
            string? element = ElementNameFor(field.Key);
            if (element is null)
            {
                continue;
            }

            changed |= SetElement(propertyGroup, element, field.Field.Value);
        }

        if (!changed)
        {
            return [];
        }

        // XDocument.Save XML-encodes element text — the operator value cannot break the project XML.
        document.Save(path, SaveOptions.DisableFormatting);
        return [relative];
    }

    private static string? ElementNameFor(string key) => key switch
    {
        SetupKeys.IdentityDescription => "Description",
        SetupKeys.IdentityRepositoryUrl => "RepositoryUrl",
        SetupKeys.IdentityLicense => "PackageLicenseExpression",
        SetupKeys.IdentityAuthors => "Authors",
        _ => null,
    };

    private static bool SetElement(XElement propertyGroup, string name, string value)
    {
        XElement? existing = propertyGroup.Elements(name).FirstOrDefault();
        if (existing is not null)
        {
            if (string.Equals(existing.Value, value, StringComparison.Ordinal))
            {
                return false;
            }

            existing.Value = value; // SetValue/Value assignment XML-encodes on Save.
            return true;
        }

        // Add a new element (e.g. <RepositoryUrl>, absent in the template) next to the existing metadata.
        propertyGroup.Add(new XElement(name, value));
        return true;
    }
}
