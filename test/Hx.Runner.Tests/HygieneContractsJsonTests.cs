using System.Text.Json;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Runner.Tests;

public sealed class HygieneContractsJsonTests
{
    [Fact]
    public void HygieneScanResultUsesCamelCaseAndStringEnums()
    {
        var result = new HygieneScanResult(
            JsonContractDefaults.SchemaVersion,
            StageOutcome.Pass,
            HygieneScope.All,
            HygieneSource.WorkingTree,
            3,
            [],
            null,
            [],
            [],
            []);

        string json = JsonSerializer.Serialize(result, JsonContractSerializerOptions.Create());

        Assert.Contains("\"schemaVersion\":1", json);
        Assert.Contains("\"scannedFileCount\":3", json);
        Assert.Contains("\"scope\":\"all\"", json);
        Assert.Contains("\"source\":\"workingTree\"", json);
        Assert.Contains("\"outcome\":\"pass\"", json);
    }

    [Fact]
    public void HygieneFindingSerializesEnumsAsCamelCaseStrings()
    {
        var finding = new HygieneFinding(
            HygieneFindingCategory.PrivateKey,
            HygieneSeverity.Error,
            "scaffold.private-key",
            "src/key.pem",
            10,
            "Private key block detected (value redacted).");

        string json = JsonSerializer.Serialize(finding, JsonContractSerializerOptions.Create());

        Assert.Contains("\"category\":\"privateKey\"", json);
        Assert.Contains("\"severity\":\"error\"", json);
    }
}
