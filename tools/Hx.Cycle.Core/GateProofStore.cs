using System.Text.Json;
using Hx.Tooling.Contracts;

namespace Hx.Cycle.Core;

/// <summary>
/// Reads/writes the persisted, change-set-bound gate proof at <c>.doti/gate-proof.json</c> (gitignored).
/// <see cref="Persist"/> binds the proof to the current change-set identity (computed against the cycle's
/// base ref, so persisted proof and transition/release verification agree). Non-forgeable freshness: a later diff
/// change moves the identity, so the stored proof reads stale.
/// </summary>
public sealed class GateProofStore
{
    public const string RelativePath = ".doti/gate-proof.json";

    private readonly string _path;

    public GateProofStore(string repositoryRoot) =>
        _path = Path.GetFullPath(Path.Combine(repositoryRoot, ".doti", "gate-proof.json"));

    public PersistedGateProof? Read()
    {
        if (!File.Exists(_path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<PersistedGateProof>(File.ReadAllText(_path), JsonContractSerializerOptions.Create());
    }

    public void Write(PersistedGateProof proof)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        JsonSerializerOptions options = JsonContractSerializerOptions.Create();
        options.WriteIndented = true;
        File.WriteAllText(_path, JsonSerializer.Serialize(proof, options));
    }

    /// <summary>Bind <paramref name="proof"/> to the current change set + persist it. Called by <c>gate run</c>
    /// after a passing run. Uses the cycle's recorded <c>BaseRef</c> if a cycle is active (so it matches what
    /// transition/release verification will verify), else the resolved default.</summary>
    public static PersistedGateProof Persist(string repositoryRoot, Lane lane, GateProof proof)
    {
        // 022 (Bug#2 fix): single-sourced with the gate's affected-test base via GitRefs.ResolveProofBaseRef, so the
        // persisted base and the affected-test proof base are always the same commit (the transition validator
        // requires it). The cycle base wins when active; else dev/HEAD as a concrete SHA.
        string baseRef = GitRefs.ResolveProofBaseRef(repositoryRoot);
        string changeSetId = ChangeSetIdentity.Of(repositoryRoot, baseRef, "HEAD");
        var persisted = new PersistedGateProof(
            JsonContractDefaults.SchemaVersion, changeSetId, baseRef, lane, proof, GitRefs.TryHeadSha(repositoryRoot));
        new GateProofStore(repositoryRoot).Write(persisted);
        return persisted;
    }
}
