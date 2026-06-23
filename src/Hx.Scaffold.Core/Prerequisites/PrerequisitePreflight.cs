using Hx.Tooling.Contracts;

namespace Hx.Scaffold.Core.Prerequisites;

public static partial class PrerequisitePreflight
{
    public static PrerequisiteCheckReport Check(
        PrerequisiteCheckRequest request,
        PrerequisiteServices? services = null)
    {
        services ??= new PrerequisiteServices();
        LoadedPrerequisiteManifest loaded = PrerequisiteManifestStore.LoadFromSourceRoot(request.SourceRoot);
        var items = new List<PrerequisiteCheckItem>();
        var blockers = new List<string>();
        var nextActions = new List<string>();

        foreach (PrerequisiteRequirement requirement in loaded.Manifest.Requirements)
        {
            string? level = LevelFor(requirement, request.Command);
            if (level is null)
            {
                continue;
            }

            PrerequisiteCheckItem item = Probe(requirement, level, request, services);
            items.Add(item);
            if (level == "hard" && item.Status != "found")
            {
                blockers.Add($"{requirement.DisplayName}: {item.Reason ?? item.Status}");
                foreach (string instruction in requirement.Instructions)
                {
                    if (!nextActions.Contains(instruction, StringComparer.Ordinal))
                    {
                        nextActions.Add(instruction);
                    }
                }
            }
        }

        IReadOnlyList<PrerequisiteDirectoryCheck> directories = CheckDirectories(request);
        foreach (PrerequisiteDirectoryCheck directory in directories.Where(d => !d.Ok))
        {
            blockers.Add($"{directory.Id}: {directory.Reason}");
        }

        PrerequisiteInstallPlan? plan = BuildInstallPlan(request.Command, loaded.Sha256, items, services);
        return new PrerequisiteCheckReport(
            JsonContractDefaults.SchemaVersion,
            request.Command,
            loaded.Path,
            loaded.Sha256,
            blockers.Count == 0,
            items,
            directories,
            blockers,
            nextActions,
            plan,
            []);
    }
}
