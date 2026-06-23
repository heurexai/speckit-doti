using System.CommandLine;
using Hx.Cli.Kernel;

namespace Hx.Runner.Cli;

internal static partial class RunnerCommandFactory
{
    public static RootCommand Create(CliMeta meta)
    {
        RootCommand rootCommand = new("scaffold-dotnet deterministic runner");
        AddBootstrap(rootCommand, meta);
        AddPlatform(rootCommand, meta);
        AddHygiene(rootCommand, meta);
        AddSentrux(rootCommand, meta);
        AddDoti(rootCommand, meta);
        AddArchitecture(rootCommand, meta);
        AddGate(rootCommand, meta);
        AddVersion(rootCommand, meta);
        AddSecurity(rootCommand, meta);
        AddTools(rootCommand, meta);
        AddErrorCodes(rootCommand, meta);
        CliApp.AddDescribe(rootCommand, meta, ErrorCodes.All);
        return rootCommand;
    }
}
