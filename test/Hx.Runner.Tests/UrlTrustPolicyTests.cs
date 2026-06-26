using System.Net;
using Hx.Security.Core;
using Xunit;

namespace Hx.Runner.Tests;

/// <summary>
/// 007 T034 (FR-035 / SC-014): the SSRF-resistant URL trust policy refuses non-https, non-allowlisted hosts, and any
/// host that resolves to a private/reserved address — defending redirect, DNS-rebind, and encoded-IP bypasses.
/// </summary>
public sealed class UrlTrustPolicyTests
{
    private static readonly IReadOnlySet<string> Allow = new HashSet<string> { "docs.example.com" };
    private static IEnumerable<IPAddress> ResolvePublic(string _) => [IPAddress.Parse("93.184.216.34")];
    private static IEnumerable<IPAddress> ResolveLoopback(string _) => [IPAddress.Parse("127.0.0.1")];

    [Theory]
    [InlineData("http://docs.example.com/x")]
    [InlineData("ftp://docs.example.com/x")]
    [InlineData("file:///etc/passwd")]
    [InlineData("data:text/html,<x>")]
    public void Non_https_is_refused(string url)
    {
        UrlTrustDecision decision = UrlTrustPolicy.Validate(url, Allow, ResolvePublic);

        Assert.False(decision.Allowed);
        Assert.Equal(UrlBlockReason.NotHttps, decision.Reason);
    }

    [Fact]
    public void Host_not_in_the_allowlist_is_refused_with_a_sanitized_host_diagnostic()
    {
        UrlTrustDecision decision = UrlTrustPolicy.Validate("https://evil.test/secret?token=abc", Allow, ResolvePublic);

        Assert.False(decision.Allowed);
        Assert.Equal(UrlBlockReason.HostNotAllowed, decision.Reason);
        Assert.Equal("evil.test", decision.Host); // host only — never the raw URL / query / credentials
    }

    [Theory]
    [InlineData("https://2130706433/")] // decimal-encoded 127.0.0.1 host
    [InlineData("https://0x7f000001/")] // hex-encoded host
    public void Encoded_ip_host_is_refused_by_the_allowlist(string url)
    {
        UrlTrustDecision decision = UrlTrustPolicy.Validate(url, Allow, ResolvePublic);

        Assert.False(decision.Allowed);
        Assert.Equal(UrlBlockReason.HostNotAllowed, decision.Reason);
    }

    [Fact]
    public void Dns_rebind_to_a_private_address_is_refused()
    {
        // The host IS allowlisted, but resolves to loopback (rebind). Every resolved address is checked.
        UrlTrustDecision decision = UrlTrustPolicy.Validate("https://docs.example.com/x", Allow, ResolveLoopback);

        Assert.False(decision.Allowed);
        Assert.Equal(UrlBlockReason.PrivateOrReservedAddress, decision.Reason);
    }

    [Fact]
    public void An_allowlisted_private_ip_literal_host_is_still_refused()
    {
        var allow = new HashSet<string> { "192.168.1.1" };

        UrlTrustDecision decision = UrlTrustPolicy.Validate("https://192.168.1.1/x", allow, ResolvePublic);

        Assert.False(decision.Allowed);
        Assert.Equal(UrlBlockReason.PrivateOrReservedAddress, decision.Reason);
    }

    [Theory]
    [InlineData("192.168.1.1")]
    [InlineData("10.0.0.1")]
    [InlineData("172.16.0.1")]
    [InlineData("127.0.0.1")]
    [InlineData("169.254.169.254")] // cloud metadata endpoint
    [InlineData("100.64.0.1")]      // CGNAT
    [InlineData("0.0.0.0")]
    [InlineData("::1")]
    [InlineData("fc00::1")]          // unique-local
    [InlineData("fe80::1")]          // link-local
    [InlineData("::ffff:127.0.0.1")] // IPv4-mapped loopback
    public void Private_and_reserved_addresses_are_blocked(string ip) =>
        Assert.True(UrlTrustPolicy.IsBlockedAddress(IPAddress.Parse(ip)));

    [Theory]
    [InlineData("93.184.216.34")]
    [InlineData("8.8.8.8")]
    [InlineData("2001:4860:4860::8888")]
    public void Public_addresses_are_allowed(string ip) =>
        Assert.False(UrlTrustPolicy.IsBlockedAddress(IPAddress.Parse(ip)));

    [Fact]
    public void An_allowlisted_https_host_resolving_public_is_allowed_and_pins_the_address()
    {
        UrlTrustDecision decision = UrlTrustPolicy.Validate("https://docs.example.com/guide", Allow, ResolvePublic);

        Assert.True(decision.Allowed);
        Assert.Equal("docs.example.com", decision.Host);
        Assert.Contains("93.184.216.34", decision.PinnedAddresses);
    }

    [Fact]
    public void A_redirect_hop_is_re_validated_and_a_bad_target_is_refused()
    {
        // A fetcher disables auto-redirect and re-validates each hop — the redirect target is just another Validate.
        Assert.True(UrlTrustPolicy.Validate("https://docs.example.com/redirect", Allow, ResolvePublic).Allowed);
        Assert.False(UrlTrustPolicy.Validate("http://docs.example.com/internal", Allow, ResolvePublic).Allowed);
        Assert.False(UrlTrustPolicy.Validate("https://evil.test/internal", Allow, ResolvePublic).Allowed);
    }
}
