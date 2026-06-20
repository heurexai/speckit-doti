using Hx.Tooling.Contracts;

namespace Hx.Doti.Core;

/// <summary>One rendered output file (relative repo path + its content).</summary>
public sealed record DotiRenderTarget(string RelativePath, string Content);

/// <summary>Per-file render/check status.</summary>
public sealed record DotiRenderFileStatus(string Path, bool Matches, bool Existed);

/// <summary>
/// JSON proof for <c>doti render-skills</c>. In <c>--check</c> mode the outcome is
/// <see cref="StageOutcome.Fail"/> when any installed file differs from the render (fail
/// closed); in write mode it lists what was (re)written.
/// </summary>
public sealed record DotiRenderResult(
    int SchemaVersion,
    StageOutcome Outcome,
    bool CheckMode,
    IReadOnlyList<DotiRenderFileStatus> Files,
    IReadOnlyList<string> Drifted,
    IReadOnlyList<string> Written);
