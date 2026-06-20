using Hx.Tooling.Contracts;

namespace Hx.Runner.Core;

public static class GateProofFactory
{
    public static GateProof BootstrapAdvisoryProof()
    {
        GateEvidence evidence = new("phase", "The solution spine is command-aware advisory.");
        GateStep step = new("solution-spine", StageOutcome.Pass, [evidence]);

        return new GateProof(JsonContractDefaults.SchemaVersion, StageOutcome.Pass, [step], [evidence]);
    }
}
