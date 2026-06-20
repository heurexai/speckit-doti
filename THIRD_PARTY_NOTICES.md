# Third-Party Notices

speckit-doti is MIT-licensed. The repository also references, vendors, or integrates with third-party projects.

## Vendored or pinned tools

- Gitleaks: MIT license. See `tools/gitleaks/LICENSE` and `tools/gitleaks/gitleaks.version.json`.
- Sentrux Heurex fork: MIT license. See `tools/sentrux/LICENSE` and `tools/sentrux/sentrux.version.json`.
- GitVersion: MIT license. See `tools/gitversion/LICENSE` and `tools/gitversion/gitversion.version.json`.

Large tool binaries are intentionally not committed unless they are deliberately vendored with a manifest and hash proof.

## NuGet packages

Built against these third-party NuGet packages (restored at build time; not redistributed in this repository):

- System.CommandLine — MIT license (https://github.com/dotnet/command-line-api).
- xUnit.net (`xunit.v3`, `xunit.runner.visualstudio`) — Apache-2.0 license (https://github.com/xunit/xunit).
- Microsoft.NET.Test.Sdk — MIT license (https://github.com/microsoft/vstest).
- ArchUnitNET (`TngTech.ArchUnitNET.xUnitV3`) — Apache-2.0 license (https://github.com/TNG/ArchUnitNET).

## Workflow assets

The Doti workflow source assets under `doti/` are original scaffold-dotnet content written for the Heurex scaffold workflow unless a file says otherwise.

If future work copies or adapts upstream project material, record the source project, license, copyright holder, source URL, and copied or adapted file list here before release.

