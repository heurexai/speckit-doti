using Hx.Tooling.Contracts.Setup;

namespace Hx.Doti.Core.Setup;

/// <summary>029 D3: assembles the <c>.doti</c>-asset writer set the <see cref="SetupConfigProjector"/> consumes —
/// the Doti.Core writers reachable by <c>hx new</c> (Scaffold.Core→Doti.Core), <c>hx doti install</c> (Doti.Core),
/// and <c>config show</c> (Runner.Cli→Doti.Core) with no new edge. The <c>CsprojMetadata</c> writer needs the project
/// name to locate <c>src/{name}.Cli/{name}.Cli.csproj</c>; the others are repo-root relative.</summary>
public static class SetupTargetWriters
{
    /// <summary>The full new-path writer set (csproj metadata + GitVersion seed + release env-var + constitution §2).</summary>
    public static IReadOnlyDictionary<SetupTarget, ISetupTargetWriter> ForNew(string projectName) =>
        Build(new CsprojMetadataWriter(projectName));

    /// <summary>The install-subset writer set: the doti-layer assets (GitVersion seed + release env-var + constitution
    /// §2). The csproj-metadata writer is omitted — <c>hx doti install</c> never regenerates an existing repo's projects
    /// (C3), so identity.* csproj fields are reported as ignored on that path.</summary>
    public static IReadOnlyDictionary<SetupTarget, ISetupTargetWriter> ForInstall() =>
        new Dictionary<SetupTarget, ISetupTargetWriter>
        {
            [SetupTarget.GitVersionSeed] = new GitVersionSeedWriter(),
            [SetupTarget.ReleaseManifest] = new ReleaseTargetWriter(),
            [SetupTarget.ConstitutionSection2] = new ConstitutionSection2Writer(),
        };

    private static IReadOnlyDictionary<SetupTarget, ISetupTargetWriter> Build(CsprojMetadataWriter csproj) =>
        new Dictionary<SetupTarget, ISetupTargetWriter>
        {
            [SetupTarget.CsprojMetadata] = csproj,
            [SetupTarget.GitVersionSeed] = new GitVersionSeedWriter(),
            [SetupTarget.ReleaseManifest] = new ReleaseTargetWriter(),
            [SetupTarget.ConstitutionSection2] = new ConstitutionSection2Writer(),
        };
}
