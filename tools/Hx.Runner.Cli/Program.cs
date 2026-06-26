using System.CommandLine;
using System.Reflection;
using Hx.Cli.Kernel;
using Hx.Runner.Cli;

CliMeta meta = new("hx-runner", CliApp.ResolveVersion(Assembly.GetExecutingAssembly()));
RootCommand rootCommand = RunnerCommandFactory.Create(meta);

return CliApp.Harden(rootCommand, meta, args, "speckit-doti", "deterministic runner and workflow gate");
