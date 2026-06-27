using System.CommandLine;
using System.Reflection;
using Hx.Cli.Kernel;
using Hx.Semantic.Cli;

CliMeta meta = new("hx-semantic", CliApp.ResolveVersion(Assembly.GetExecutingAssembly()));
RootCommand rootCommand = SemanticCommandFactory.Create(meta);

return CliApp.Harden(rootCommand, meta, args, "speckit-doti", "advisory semantic drift finder (dev-only)");
