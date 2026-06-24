using System.Text.RegularExpressions;
using Hx.Tooling.Contracts;

namespace Hx.Scaffold.Core.Release;

public static class LocalReleaseRootResolver
{
    public const string DefaultEnvironmentVariableName = "DOTI_RELEASE_ROOT";

    private static readonly Regex EnvironmentVariableNamePattern =
        new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    public static bool IsValidEnvironmentVariableName(string name) =>
        !string.IsNullOrWhiteSpace(name) && EnvironmentVariableNamePattern.IsMatch(name);

    public static LocalReleaseRootDecision Resolve(
        string? explicitReleaseRoot,
        string? requestedEnvironmentVariableName,
        Func<string, string?> readEnvironmentVariable,
        string defaultEnvironmentVariableName = DefaultEnvironmentVariableName)
    {
        if (!IsValidEnvironmentVariableName(defaultEnvironmentVariableName))
        {
            throw new InvalidOperationException(
                $"Invalid default release-root environment variable name '{defaultEnvironmentVariableName}'.");
        }

        string? requested = string.IsNullOrWhiteSpace(requestedEnvironmentVariableName)
            ? null
            : requestedEnvironmentVariableName.Trim();
        string effective = requested ?? defaultEnvironmentVariableName;

        if (!string.IsNullOrWhiteSpace(explicitReleaseRoot))
        {
            return new LocalReleaseRootDecision(
                effective,
                requested,
                EnvironmentVariableRead: false,
                EnvironmentVariableIgnored: requested is not null,
                Source: "explicit",
                ReleaseRoot: explicitReleaseRoot.Trim(),
                Reason: requested is null ? null : "explicit release root overrides environment-variable lookup");
        }

        string? value = readEnvironmentVariable(effective);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return new LocalReleaseRootDecision(
                effective,
                requested,
                EnvironmentVariableRead: true,
                EnvironmentVariableIgnored: false,
                Source: requested is null ? "default-environment" : "named-environment",
                ReleaseRoot: value.Trim(),
                Reason: null);
        }

        return new LocalReleaseRootDecision(
            effective,
            requested,
            EnvironmentVariableRead: true,
            EnvironmentVariableIgnored: false,
            Source: "unavailable",
            ReleaseRoot: null,
            Reason: $"environment variable '{effective}' is not set");
    }
}
