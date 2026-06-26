using Hx.Cli.Kernel;
using Hx.Security.Core;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Cli;

public static partial class RunnerCommands
{
    // 007 T034 (FR-035 / SC-014): validate a URL for SSRF-resistant ingestion; fail closed with Validation_UrlBlocked.
    public static CliResult SecurityUrlCheck(CliMeta meta, string url, string allowCsv)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return Usage(meta, "security url-check", "--url is required.");
        }

        var allow = new HashSet<string>(
            allowCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.OrdinalIgnoreCase);
        UrlTrustDecision decision = UrlTrustPolicy.Validate(url, allow);

        if (!decision.Allowed)
        {
            // Sanitized diagnostic: the host + reason, never the raw URL.
            return CliResults.Fail(meta, "security url-check", ExitClass.Validation,
                [Diag.Of(ErrorCodes.Validation_UrlBlocked, $"URL refused ({decision.Reason}, host={decision.Host}): {decision.Detail}")],
                data: decision);
        }

        return CliResults.Ok(meta, "security url-check",
            $"URL allowed for host {decision.Host} (pin to {decision.PinnedAddresses.Count} resolved address(es); disable redirects and treat the response as untrusted data).",
            decision);
    }
}
