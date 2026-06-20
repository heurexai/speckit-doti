using Hx.Runner.Core;
using Hx.Tooling.Contracts;
using System.Text.Json;
using Xunit;

namespace Hx.Runner.Tests;

public sealed class GateProofFactoryTests
{
    [Fact]
    public void BootstrapAdvisoryProofPasses()
    {
        var proof = GateProofFactory.BootstrapAdvisoryProof();

        Assert.Equal(StageOutcome.Pass, proof.Outcome);
        Assert.Single(proof.Steps);
    }

    [Fact]
    public void ContractJsonUsesCamelCaseAndStringEnums()
    {
        var proof = GateProofFactory.BootstrapAdvisoryProof();

        string json = JsonSerializer.Serialize(proof, JsonContractSerializerOptions.Create());

        Assert.Contains("\"schemaVersion\"", json);
        Assert.Contains("\"outcome\":\"pass\"", json);
    }
}
