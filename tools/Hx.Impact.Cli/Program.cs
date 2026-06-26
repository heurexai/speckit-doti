using System.Reflection;
using Hx.Cli.Kernel;
using Hx.Impact.Cli;

CliMeta meta = new("hx-impact", CliApp.ResolveVersion(Assembly.GetExecutingAssembly()));
var rootCommand = ImpactCommandFactory.Create(meta);

return CliApp.Harden(rootCommand, meta, args, "speckit-doti", "deterministic affected-test planner");
