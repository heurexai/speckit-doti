# speckit-doti — Privacy Policy

_Last updated: 2026-06-21_

speckit-doti ("the software") is an open-source, MIT-licensed command-line developer tool
published by Heurex. It scaffolds .NET projects and runs quality and architecture checks
on your own machine.

## Data we collect

**None.** speckit-doti does not collect, store, or transmit any personal data. It contains
no telemetry, analytics, tracking, advertising identifiers, or user accounts, and it does
not "phone home."

## How it uses your device

The software runs entirely on your local machine. It:

- creates and modifies files in the project directory you specify;
- stores downloaded developer-tool binaries in a local cache (for example,
  `%LOCALAPPDATA%\Heurex\speckit-doti` on Windows, or the platform's user data directory);
- invokes tools you already have (the .NET SDK, Git) to build and check your code.

None of this information is sent anywhere by the software.

## Network access

The only network activity the software initiates is downloading its pinned,
SHA-256-verified developer-tool binaries (Gitleaks, Sentrux, GitVersion) from their
official release locations on GitHub when they are not already present locally. These are
ordinary file downloads; the software transmits no personal data with them beyond the
standard information any HTTP request necessarily includes (such as your IP address, which
is visible to the host serving the download). Any version-control operations are ones you
run yourself, against remotes you choose.

## Third parties

Downloads are served by GitHub and the respective tool publishers; when the software is
installed via the Microsoft Store, distribution is handled by Microsoft. Their handling of
request data is governed by their own privacy statements (for example, the Microsoft
Privacy Statement and the GitHub Privacy Statement). speckit-doti itself shares no data
with any third party.

## Children's privacy

The software is a developer tool, is not directed at children, and collects no data from
anyone.

## Changes to this policy

Any updates will be published at this URL with a revised "Last updated" date.

## Contact

Questions about this policy: open an issue at
<https://github.com/heurexai/speckit-doti/issues>.
