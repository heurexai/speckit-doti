# Publishing speckit-doti

`hx` ships through **two channels**, both built from the same source-free package — per-RID tool
binaries (Gitleaks/Sentrux/GitVersion) are fetched and hash-verified on demand, never bundled. Both
publish automatically when a `v*` tag is pushed: `hx release` creates the tag locally; pushing it
triggers CI.

## 1. .NET global tool (NuGet.org) — primary, all OSes

`.github/workflows/release.yml` packs `Heurex.SpeckitDoti` (a framework-dependent .NET global tool),
runs a **source-free install smoke** (`dotnet tool install` into a clean path, then `hx new` / `hx doti
install` with no source checkout on `PATH`), and publishes to NuGet.org via **Trusted Publishing**
(GitHub OIDC — no stored API key).

```bash
dotnet tool install --global Heurex.SpeckitDoti     # install
dotnet tool update  --global Heurex.SpeckitDoti     # update
```

**One-time operator prep** (before the first publish — cannot be done from CI):

1. NuGet.org: create a Trusted Publishing policy (owner `heurexai`, repo `speckit-doti`, workflow `release.yml`).
2. Add the `NUGET_USER` repo secret = the nuget.org **username** (not the email).
3. Configure the `release` Environment with a **required reviewer** (gates OIDC issuance — a token is never
   minted on just any collaborator's tag push).
4. **Pin every action** in `release.yml` to a full commit SHA (the workflow is authored with floating major
   tags as placeholders; resolve each from the action's verified release).

## 2. Microsoft Store (MSIX) — Windows

`.github/workflows/store-release.yml` builds and submits the signed MSIX. The Store signs it with a
Microsoft-trusted certificate, so a Store-installed `hx` runs with **no SmartScreen prompt**, and it also
surfaces via `winget install Heurex.speckit-doti` (the `msstore` source). Product identity, Partner Center /
Azure AD setup, and the `STORE_*` secrets are in **[STORE.md](STORE.md)**. This channel is Windows-only;
other OSes use the global tool above.

## Signing

The **Store MSIX is signed** by the Store. The NuGet global tool's assemblies are unsigned (the tool runs
under the .NET host, which the user already trusts); Authenticode for a standalone Windows `hx.exe` outside
the Store is deferred until a certificate is available.
