using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hx.Tooling.Contracts;

namespace Hx.Cycle.Core;

internal static class CycleStageProofHasher
{
    public static string? HashPrerequisites(CycleState? state, IReadOnlyList<string> prereqStageIds)
    {
        if (prereqStageIds.Count == 0)
        {
            return null;
        }

        if (state is null)
        {
            return null;
        }

        var required = new HashSet<string>(prereqStageIds, StringComparer.OrdinalIgnoreCase);
        var proofs = state.Stages
            .Where(s => required.Contains(s.Stage))
            .OrderBy(s => s.Stage, StringComparer.OrdinalIgnoreCase)
            .Select(s => new
            {
                s.Stage,
                s.Outcome,
                s.ChangeSetId,
                ArtifactHashes = s.ArtifactHashes.OrderBy(h => h, StringComparer.Ordinal).ToArray(),
                s.StampedAtCommit,
                s.PrerequisiteProofHash,
            })
            .ToArray();

        if (proofs.Length != prereqStageIds.Count)
        {
            return null;
        }

        string json = JsonSerializer.Serialize(proofs, JsonContractSerializerOptions.Create());
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
