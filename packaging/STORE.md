# Microsoft Store (MSIX) publishing

The Store signs the submitted MSIX with a Microsoft-trusted certificate, so a Store-installed
`speckit-doti` runs **with no SmartScreen warning** — without buying an Authenticode cert. It also
surfaces via `winget` (the `msstore` source). This covers **Windows** only; Linux/macOS use the
release archives. The Store signs the **MSIX**, not the standalone GitHub `.zip`.

## Reserved product identity (Partner Center → Product identity)

| Field | Value |
| --- | --- |
| Package/Identity/Name | `Heurex.speckit-doti` |
| Package/Identity/Publisher | `CN=44BD9E29-FD96-49A3-87CD-DCD53760D855` |
| Publisher display name | `Heurex` |
| Store ID | `9P4FZG3SQ890` |

These are wired into `packaging/msix/AppxManifest.xml`. **Reminder:** the name reservation lapses if
the first submission isn't made within ~3 months of reserving.

## One-time setup (manual, in Partner Center / Azure)

1. **Complete the listing** for the product: description, screenshots, category, **age rating (IARC)**,
   privacy-policy URL. (Required before any submission can pass certification.)
2. **Link an Azure AD app for API submission**: Partner Center → *Account settings → User management →
   Azure AD applications* → add an app (e.g. `speckit-doti-ci`) with the **Developer** role (least
   privilege — it can upload packages and submit apps/add-ons, but cannot touch account or financial
   settings; elevate to **Manager** only if a submission ever fails with an insufficient-permissions
   error). In the Azure portal create a **client secret** for it (note its expiry — rotate before then).
3. Add repo **Actions secrets**: `STORE_TENANT_ID`, `STORE_SELLER_ID` (Partner Center seller ID),
   `STORE_CLIENT_ID`, `STORE_CLIENT_SECRET`.
4. **Do the first submission manually** — build the MSIX (below), upload it in Partner Center, complete
   the listing, and submit. The submission API/CLI automates *subsequent* versions, not the first.

## Build the MSIX locally (for the first submission / testing)

Build from the matching release tag/ref. The packaged `hx.exe --version` must exactly match the
MSIX version prefix (for example, `0.4.0` in an MSIX identity version of `0.4.0.0`); the Store
workflow enforces this before packing.

```powershell
dotnet publish tools/Hx.Scaffold.Cli -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
$layout = "msix-layout"; ni -ItemType Directory -Force $layout | Out-Null
git archive --format=tar HEAD | tar -x -C $layout
foreach ($t in 'gitleaks','sentrux','gitversion') { copy -Recurse -Force "tools/$t/bin" "$layout/tools/$t/bin" }
copy publish/Hx.Scaffold.Cli.exe "$layout/hx.exe"
copy packaging/msix/AppxManifest.xml "$layout/AppxManifest.xml"
ni -ItemType Directory -Force "$layout/Assets" | Out-Null; copy packaging/msix/Assets/* "$layout/Assets/"
& "${env:ProgramFiles(x86)}\Windows Kits\10\bin\<sdk-version>\x64\makeappx.exe" pack /d $layout /p speckit-doti-0.4.0.msix /o
```
The MSIX bundles `hx` + its payload (template, doti, source, manifests, vendored grammars) + the win-x64
tool binaries; `hx` resolves its payload from the package install dir (read-only — generated projects and
the tool store go to writable user locations). The `hx` command is exposed via an AppExecutionAlias.

## Subsequent versions (automated)

Run the **Store release** workflow (`.github/workflows/store-release.yml`, `workflow_dispatch`) with the
version — it builds the MSIX and submits via the `msstore` CLI using the secrets above. Verify the exact
`msstore` flags against [its docs](https://learn.microsoft.com/windows/apps/publish/msstore-dev-cli/overview)
on the first automated run.

## Notes
- The `Assets/*.png` tile logos are the **Heurex primary mark** on the navy brand background. The Store
  *listing* images (screenshots, promotional art) are uploaded separately in Partner Center.
- Certification/review is Microsoft-side and not instant.
- None of this is *required* to ship — the GitHub Release archives already work unsigned; the Store path
  is the warning-free Windows install.
