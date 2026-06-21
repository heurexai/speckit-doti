# Publishing speckit-doti

The release workflow (`.github/workflows/release.yml`) builds, on each `v*` tag, a standalone
installer per RID and attaches it to the GitHub Release:

| RID | Archive |
| --- | --- |
| win-x64 | `speckit-doti-<version>-win-x64.zip` |
| linux-x64 | `speckit-doti-<version>-linux-x64.tar.gz` |
| osx-arm64 | `speckit-doti-<version>-osx-arm64.tar.gz` |

Each archive is a self-contained `hx`/`hx.exe` plus the scaffold payload (template, doti, source,
manifests, vendored grammars) and that RID's tool binaries. Download, extract, run `hx new …`.

## Package managers (post-release, external)

winget and Homebrew install **by reference to the published archives**, so they can only be updated
**after** a release exists (their manifests pin the archive's SHA-256). Both also live in **external
repos**. The templates here are the source of truth; publishing is a manual post-release step.

### Get the published archive hashes

```bash
v=v0.2.0
for a in win-x64.zip linux-x64.tar.gz osx-arm64.tar.gz; do
  f="speckit-doti-$v-$a"
  gh release download "$v" --repo heurexai/speckit-doti --pattern "$f"
  echo "$f  $(sha256sum "$f" | cut -d' ' -f1)"
done
```

### winget (`microsoft/winget-pkgs`)

1. Fill `PackageVersion`, `InstallerUrl`, and `InstallerSha256` (win-x64 zip) in `winget/*.yaml`.
2. Validate: `winget validate --manifest packaging/winget` and (optionally) `winget install --manifest packaging/winget`.
3. Submit the three manifests under `manifests/h/Heurex/SpeckitDoti/<version>/` via a PR to
   [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs) (or `wingetcreate submit`).
   Then: `winget install Heurex.SpeckitDoti`.

### Homebrew (`heurexai/homebrew-tap`)

1. Create the tap repo `heurexai/homebrew-tap` once (a public repo with a `Formula/` dir).
2. Fill the two `sha256` values (osx-arm64 + linux-x64 tarballs) in `homebrew/speckit-doti.rb`.
3. Copy it to `Formula/speckit-doti.rb` in the tap, `brew audit --new --formula speckit-doti`, and push.
   Then: `brew install heurexai/tap/speckit-doti`.

## Signing (deferred)

The archives and `hx` are unsigned. Authenticode (Windows) and Apple notarization (macOS) are deferred
until certificates are available; until then macOS Gatekeeper / Windows SmartScreen may warn on first run.
