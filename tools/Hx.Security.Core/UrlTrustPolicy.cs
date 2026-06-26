using System.Net;
using System.Net.Sockets;

namespace Hx.Security.Core;

/// <summary>Why a URL was refused for ingestion (FR-035 / SC-014). The CLI maps this to <c>Validation_UrlBlocked</c>.</summary>
public enum UrlBlockReason
{
    None,
    NotHttps,
    HostNotAllowed,
    UnresolvableHost,
    PrivateOrReservedAddress,
}

/// <summary>
/// The outcome of validating a URL for SSRF-resistant ingestion. <see cref="Host"/> is the ONLY identifying detail
/// (sanitized — never the raw URL, query, or credentials). <see cref="PinnedAddresses"/> are the resolved IPs a
/// fetcher must pin its connection to so a name cannot rebind to a different address between check and fetch.
/// </summary>
public sealed record UrlTrustDecision(
    bool Allowed,
    UrlBlockReason Reason,
    string Host,
    IReadOnlyList<string> PinnedAddresses,
    string? Detail);

/// <summary>
/// 007 T034 (FR-035 / SC-014): an SSRF-resistant URL trust policy for any doti command that ingests a URL — NOT a
/// literal blocklist. A URL is allowed only when it is <c>https</c>, its host is in an explicit allowlist, AND every
/// resolved address is a public unicast address (by <see cref="IPAddress"/> category, not string match). Callers:
/// <list type="bullet">
///   <item>refuse <c>http</c>/<c>file:</c> and anything non-https;</item>
///   <item>connect to the returned <see cref="UrlTrustDecision.PinnedAddresses"/> only (closes DNS-rebinding);</item>
///   <item>disable auto-redirect and re-validate each hop by calling <see cref="Validate"/> on the redirect target;</item>
///   <item>treat fetched content as untrusted DATA, never instructions, and log only the host.</item>
/// </list>
/// </summary>
public static class UrlTrustPolicy
{
    /// <summary>Validate one URL (one redirect hop). Inject <paramref name="resolve"/> in tests to model DNS rebinding.</summary>
    public static UrlTrustDecision Validate(
        string url, IReadOnlySet<string> allowedHosts, Func<string, IEnumerable<IPAddress>>? resolve = null)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            return Block(UrlBlockReason.NotHttps, "(unparseable)", "URL is not an absolute URI.");
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            // Refuses http, file:, ftp:, data:, and every non-https scheme.
            return Block(UrlBlockReason.NotHttps, uri.Host, $"scheme '{uri.Scheme}' is not https.");
        }

        string host = uri.Host;
        if (allowedHosts is null || !allowedHosts.Contains(host))
        {
            return Block(UrlBlockReason.HostNotAllowed, host, "host is not in the allowlist.");
        }

        IReadOnlyList<IPAddress> addresses;
        try
        {
            addresses = ResolveAll(uri, resolve);
        }
        catch (SocketException)
        {
            return Block(UrlBlockReason.UnresolvableHost, host, "host did not resolve.");
        }

        if (addresses.Count == 0)
        {
            return Block(UrlBlockReason.UnresolvableHost, host, "host resolved to no addresses.");
        }

        // Reject if ANY resolved address is private/reserved — defends DNS rebinding and numeric/encoded-IP hosts that
        // resolve to an internal address.
        foreach (IPAddress ip in addresses)
        {
            if (IsBlockedAddress(ip))
            {
                return Block(UrlBlockReason.PrivateOrReservedAddress, host, "resolves to a private or reserved address.");
            }
        }

        return new UrlTrustDecision(true, UrlBlockReason.None, host, addresses.Select(a => a.ToString()).ToArray(), null);
    }

    /// <summary>True when an address is loopback, link-local (incl. the 169.254.169.254 metadata endpoint), RFC1918,
    /// CGNAT (100.64/10), unique-local (fc00::/7), unspecified, multicast/reserved, or an IPv4-mapped form of any of
    /// those — categorized by <see cref="IPAddress"/>, never by string comparison.</summary>
    public static bool IsBlockedAddress(IPAddress address)
    {
        IPAddress ip = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        if (IPAddress.IsLoopback(ip))
        {
            return true; // 127.0.0.0/8, ::1
        }

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            byte[] b = ip.GetAddressBytes();
            return b[0] == 10                                 // 10.0.0.0/8
                || (b[0] == 172 && b[1] is >= 16 and <= 31)   // 172.16.0.0/12
                || (b[0] == 192 && b[1] == 168)               // 192.168.0.0/16
                || (b[0] == 169 && b[1] == 254)               // 169.254.0.0/16 link-local (incl. cloud metadata)
                || (b[0] == 100 && b[1] is >= 64 and <= 127)  // 100.64.0.0/10 CGNAT
                || b[0] == 0                                  // 0.0.0.0/8 "this host"
                || b[0] >= 224;                               // 224.0.0.0/4 multicast + 240.0.0.0/4 reserved
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast)
            {
                return true;
            }

            return (ip.GetAddressBytes()[0] & 0xFE) == 0xFC;  // fc00::/7 unique-local
        }

        return true; // unknown address family — fail closed
    }

    private static IReadOnlyList<IPAddress> ResolveAll(Uri uri, Func<string, IEnumerable<IPAddress>>? resolve)
    {
        // An IP-literal host (dotted-quad / bracketed IPv6) is categorized directly — no DNS needed.
        if (uri.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6 && IPAddress.TryParse(uri.Host, out IPAddress? literal))
        {
            return [literal];
        }

        IEnumerable<IPAddress> resolved = resolve is not null ? resolve(uri.Host) : Dns.GetHostAddresses(uri.Host);
        return resolved.ToArray();
    }

    private static UrlTrustDecision Block(UrlBlockReason reason, string host, string detail) =>
        new(false, reason, host, [], detail);
}
