using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Hx.Impact.Core.Domain;
using Hx.Tooling.Contracts;

namespace Hx.Impact.Core.Planning;

/// <summary>
/// 012 (FR-003/004/005, M3): the measurable affected-test inventory. Project totals are cheap — selected vs total
/// <b>test projects</b> from the project graph (the architecture-test project is excluded so the unit-test total is
/// not inflated, FR-012). Class/case counts are reflected from the already-built <b>selected</b> test assemblies
/// only (an <c>[Fact]</c>/<c>[Theory]</c> method = a case; its declaring type = a class). The repo-wide class/case
/// <b>total</b> is honestly <c>null</c> with an <see cref="AffectedTestInventory.UnknownReason"/> — it is NEVER
/// computed by building unaffected test projects (the clarify decision). Assemblies are read via the BCL
/// <see cref="MetadataReader"/> (no execution, no assembly load), so a bad/missing DLL yields <c>unknown</c>, never
/// a crash. Telemetry only — never a proof input (M1).
/// </summary>
public static class AffectedTestInventoryProjector
{
    // The architecture-test project is filtered out of the gate's normal unit-test run; exclude it from the unit-test
    // inventory total too (FR-012) so the denominator reflects what the gate counts as unit tests.
    private const string ArchitectureTestMarker = "Architecture.Tests";

    /// <summary>
    /// Build the inventory for an affected/full plan. <paramref name="selectedTestProjectPaths"/> are the repo-relative
    /// <c>.csproj</c> paths the gate will run; <paramref name="configuration"/> selects the built output directory.
    /// Returns null for a docs-only / no-tests plan — there is no inventory to show.
    /// </summary>
    public static AffectedTestInventory? Build(
        string repositoryRoot,
        ProjectGraph graph,
        AffectedPlan plan,
        IReadOnlyList<string> selectedTestProjectPaths,
        string configuration)
    {
        if (plan.Outcome == AffectedOutcome.NoTestsRequired)
        {
            return null;
        }

        IReadOnlyList<string> unitTestProjects = graph.Nodes.Values
            .Where(n => n.IsTestProject && !IsArchitectureTest(n))
            .Select(n => n.Path)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var selected = new HashSet<string>(
            selectedTestProjectPaths.Select(Normalize), StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<string> selectedUnitTestProjects = unitTestProjects
            .Where(p => selected.Contains(Normalize(p)))
            .ToArray();

        int totalProjects = unitTestProjects.Count;
        int selectedProjects = selectedUnitTestProjects.Count;

        // Reflect ONLY the selected (already-built) assemblies for class/case counts. The repo-wide total stays
        // unknown unless every unit-test project was selected (a full plan) AND every assembly reflected cleanly.
        AssemblyCounts selectedCounts = CountAcross(repositoryRoot, selectedUnitTestProjects, configuration);

        bool full = selectedProjects == totalProjects && totalProjects > 0;
        (int? totalClasses, int? totalCases, string? unknownReason) = full && selectedCounts.AllReflected
            ? ((int?)selectedCounts.Classes, (int?)selectedCounts.Cases, (string?)null)
            : ((int?)null, (int?)null, UnknownTotalReason(selectedCounts));

        return new AffectedTestInventory(
            selectedProjects,
            totalProjects,
            selectedCounts.AllReflected ? selectedCounts.Cases : null,
            totalCases,
            selectedCounts.AllReflected ? selectedCounts.Classes : null,
            totalClasses,
            selectedCounts.AllReflected ? unknownReason : selectedCounts.Reason);
    }

    private static string UnknownTotalReason(AssemblyCounts counts) =>
        counts.AllReflected
            ? "repo-wide class/case total not enumerated (would require building unaffected test projects)"
            : counts.Reason ?? "test assembly discovery unavailable";

    private static bool IsArchitectureTest(ProjectNode node) =>
        node.Path.Contains(ArchitectureTestMarker, StringComparison.OrdinalIgnoreCase)
        || node.Name.Contains(ArchitectureTestMarker, StringComparison.OrdinalIgnoreCase);

    private static AssemblyCounts CountAcross(
        string repositoryRoot,
        IReadOnlyList<string> projectPaths,
        string configuration)
    {
        if (projectPaths.Count == 0)
        {
            return new AssemblyCounts(0, 0, true, null);
        }

        int classes = 0;
        int cases = 0;
        foreach (string project in projectPaths)
        {
            string? assemblyPath = LocateAssembly(repositoryRoot, project, configuration);
            if (assemblyPath is null)
            {
                return new AssemblyCounts(0, 0, false,
                    $"built test assembly not found for {Path.GetFileNameWithoutExtension(project)} (run the gate's build first)");
            }

            (int typeCount, int methodCount, string? error) = CountInAssembly(assemblyPath);
            if (error is not null)
            {
                return new AssemblyCounts(0, 0, false, error);
            }

            classes += typeCount;
            cases += methodCount;
        }

        return new AssemblyCounts(classes, cases, true, null);
    }

    // Find {projectDir}/bin/{config}/{tfm}/{AssemblyName}.dll. The TFM folder is discovered (a single net*/ dir);
    // AssemblyName defaults to the project file name. Returns null when no built output exists — caller → unknown.
    private static string? LocateAssembly(string repositoryRoot, string projectRelativePath, string configuration)
    {
        try
        {
            string projectFull = Path.GetFullPath(Path.Combine(repositoryRoot, projectRelativePath));
            string projectDir = Path.GetDirectoryName(projectFull)!;
            string assemblyName = Path.GetFileNameWithoutExtension(projectFull);
            string configBin = Path.Combine(projectDir, "bin", configuration);
            if (!Directory.Exists(configBin))
            {
                return null;
            }

            foreach (string tfmDir in Directory.GetDirectories(configBin).OrderByDescending(d => d, StringComparer.Ordinal))
            {
                string candidate = Path.Combine(tfmDir, assemblyName + ".dll");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    // Read the assembly's metadata (no execution, no assembly load) and count test cases ([Fact]/[Theory] methods)
    // and their distinct declaring types. Any reader failure is isolated to an `error` so the caller marks unknown.
    private static (int Types, int Methods, string? Error) CountInAssembly(string assemblyPath)
    {
        try
        {
            using FileStream stream = File.OpenRead(assemblyPath);
            using var pe = new PEReader(stream);
            if (!pe.HasMetadata)
            {
                return (0, 0, $"{Path.GetFileName(assemblyPath)} has no managed metadata");
            }

            MetadataReader reader = pe.GetMetadataReader();
            var testTypes = new HashSet<int>();
            int caseCount = 0;
            foreach (MethodDefinitionHandle handle in reader.MethodDefinitions)
            {
                MethodDefinition method = reader.GetMethodDefinition(handle);
                if (!HasTestAttribute(reader, method))
                {
                    continue;
                }

                caseCount++;
                testTypes.Add(MetadataTokens.GetRowNumber(method.GetDeclaringType()));
            }

            return (testTypes.Count, caseCount, null);
        }
        catch (BadImageFormatException ex)
        {
            return (0, 0, $"{Path.GetFileName(assemblyPath)} is not a valid managed assembly: {ex.Message}");
        }
        catch (IOException ex)
        {
            return (0, 0, $"could not read {Path.GetFileName(assemblyPath)}: {ex.Message}");
        }
    }

    private static bool HasTestAttribute(MetadataReader reader, MethodDefinition method)
    {
        foreach (CustomAttributeHandle handle in method.GetCustomAttributes())
        {
            string name = AttributeTypeName(reader, reader.GetCustomAttribute(handle));
            if (name is "FactAttribute" or "TheoryAttribute")
            {
                return true;
            }
        }

        return false;
    }

    // Resolve the attribute's type name without loading the type. Handles both TypeReference (the common case for
    // xunit's [Fact]/[Theory]) and TypeDefinition constructor parents.
    private static string AttributeTypeName(MetadataReader reader, CustomAttribute attribute)
    {
        switch (attribute.Constructor.Kind)
        {
            case HandleKind.MemberReference:
            {
                MemberReference member = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
                return member.Parent.Kind == HandleKind.TypeReference
                    ? reader.GetString(reader.GetTypeReference((TypeReferenceHandle)member.Parent).Name)
                    : string.Empty;
            }

            case HandleKind.MethodDefinition:
            {
                MethodDefinition ctor = reader.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor);
                return reader.GetString(reader.GetTypeDefinition(ctor.GetDeclaringType()).Name);
            }

            default:
                return string.Empty;
        }
    }

    private static string Normalize(string path) => path.Replace('\\', '/').Trim();

    private sealed record AssemblyCounts(int Classes, int Cases, bool AllReflected, string? Reason);
}
