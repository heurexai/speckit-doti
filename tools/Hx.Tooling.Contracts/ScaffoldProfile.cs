namespace Hx.Tooling.Contracts;

public sealed record ScaffoldProfile(
    string Name,
    string TemplateShortName,
    string TargetFramework);
