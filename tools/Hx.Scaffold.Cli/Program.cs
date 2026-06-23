using System.CommandLine;
using System.Reflection;
using Hx.Cli.Kernel;
using Hx.Scaffold.Cli;

CliMeta meta = new("hx-scaffold", CliApp.ResolveVersion(Assembly.GetExecutingAssembly()));
RootCommand rootCommand = ScaffoldCommandFactory.Create(meta);

return CliApp.Invoke(rootCommand, meta, args, "speckit-doti",
    "Agentic .NET spec-driven development starter kit");
