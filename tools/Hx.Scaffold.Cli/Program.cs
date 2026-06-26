using System.CommandLine;
using System.Reflection;
using Hx.Cli.Kernel;
using Hx.Scaffold.Cli;
using Velopack;

VelopackApp.Build().Run();

CliMeta meta = new("hx-scaffold", CliApp.ResolveVersion(Assembly.GetExecutingAssembly()));
RootCommand rootCommand = ScaffoldCommandFactory.Create(meta);

return CliApp.Harden(rootCommand, meta, args, "speckit-doti",
    "Agentic .NET spec-driven development starter kit");
