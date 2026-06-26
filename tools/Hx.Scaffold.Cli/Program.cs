using System.CommandLine;
using System.Reflection;
using Hx.Cli.Kernel;
using Hx.Scaffold.Cli;
using Hx.Scaffold.Core;
using Hx.Tooling.Contracts;

CliMeta meta = new("hx-scaffold", CliApp.ResolveVersion(Assembly.GetExecutingAssembly()));

// 007 T045 (FR-022/FR-042): resolve the active channel + tier ONCE so the machine-readable `describe` and the
// human `--help` header report the same layering (tier + gate ladder, channel + update mechanism).
DistributionChannelInfo channel = InstalledPayload.ResolveChannel();
CliDescribeTier? tier = InstalledPayload.ResolveTier(Directory.GetCurrentDirectory());

RootCommand rootCommand = ScaffoldCommandFactory.Create(meta, channel: channel, tier: tier);

return CliApp.Harden(rootCommand, meta, args, "speckit-doti",
    "Agentic .NET spec-driven development starter kit",
    helpContext: InstalledPayload.FormatHelpContext(tier, channel));
