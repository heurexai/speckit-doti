using System.CommandLine;
using HxScaffoldSample;

namespace HxScaffoldSample.Cli;

public static class Program
{
    public static int Main(string[] args)
    {
        RootCommand root = new("HxScaffoldSample command-line tool.");

        // ---- greet: a thin command — do the work, return a CliResult, let Agent render + exit. ----
        Option<string> nameOption = new("--name") { Description = "Who to greet." };
        Option<bool> greetJson = new("--json") { Description = "Force the JSON envelope (auto-selected when piped)." };
        Command greet = new("greet", "Print a greeting.");
        greet.Options.Add(nameOption);
        greet.Options.Add(greetJson);
        greet.SetAction(parseResult => Agent.Run("greet", () =>
        {
            string name = parseResult.GetValue(nameOption) ?? string.Empty;
            IGreetingService service = new GreetingService();
            string greeting = service.Greet(new GreetingRequest(name));
            return Agent.Ok("greet", greeting, new { greeting, name });
        }, forceJson: parseResult.GetValue(greetJson) ? true : null));
        root.Subcommands.Add(greet);

        // ---- describe: the machine-readable capability model (commands + options + exit classes). ----
        Option<bool> describeJson = new("--json") { Description = "Force the JSON envelope (auto-selected when piped)." };
        Command describe = new("describe", "Emit the machine-readable capability description.");
        describe.Options.Add(describeJson);
        describe.SetAction(parseResult => Agent.Run("describe",
            () => Agent.Describe(root, "describe"),
            forceJson: parseResult.GetValue(describeJson) ? true : null));
        root.Subcommands.Add(describe);

        return root.Parse(args).Invoke();
    }
}
