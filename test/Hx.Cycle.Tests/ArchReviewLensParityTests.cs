using System.Text.RegularExpressions;
using Hx.Cycle.Core;
using Xunit;

namespace Hx.Cycle.Tests;

/// <summary>L-1 (arch-review): the lens ids tagged in the arch-review skill's lens panel must be a SUBSET of the
/// projector's known lenses (<see cref="ReviewLens.All"/>) — so the human-facing <em>Applies-when</em> prose can
/// never name a lens the deterministic projector does not, and the two cannot silently diverge.</summary>
public sealed partial class ArchReviewLensParityTests
{
    [Fact]
    public void Arch_review_panel_lens_ids_are_a_subset_of_the_projector_lenses()
    {
        string template = File.ReadAllText(Path.Combine(
            FindRepoRoot(), ".doti", "core", "templates", "commands", "doti-arch-review.md"));

        // The lens panel tags each lens with its id as "(`kebab-id`)".
        var taggedIds = LensTagRegex().Matches(template).Select(m => m.Groups["id"].Value).ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(taggedIds);
        Assert.All(taggedIds, id => Assert.Contains(id, ReviewLens.All));
        // Every projector lens is represented in the panel (no orphan lens the skill never lists).
        Assert.All(ReviewLens.All, lens => Assert.Contains(lens, taggedIds));
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "scaffold-dotnet.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository root (scaffold-dotnet.slnx) not found.");
    }

    [GeneratedRegex(@"\(`(?<id>[a-z][a-z-]+)`\)")]
    private static partial Regex LensTagRegex();
}
