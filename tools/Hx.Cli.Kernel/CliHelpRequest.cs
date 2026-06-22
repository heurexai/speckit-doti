using System.CommandLine;

namespace Hx.Cli.Kernel;

internal sealed record CliHelpRequest(Command Command, IReadOnlyList<string> Path, CliHelpMode Mode);

internal static class CliHelpRequestParser
{
    private static readonly StringComparer TokenComparer = StringComparer.OrdinalIgnoreCase;

    public static CliHelpRequest? TryParse(RootCommand root, string[] args)
    {
        ModeParse mode = ParseMode(args);
        IReadOnlyList<string> tokens = mode.Remaining;
        if (tokens.Count == 0)
        {
            return new CliHelpRequest(root, [], mode.Mode);
        }

        int helpIndex = IndexOfHelp(tokens);
        if (helpIndex < 0)
        {
            return null;
        }

        List<string> pathTokens = PathTokens(tokens, helpIndex);
        Command command = Resolve(root, pathTokens);
        return new CliHelpRequest(command, PathFrom(root, command), mode.Mode);
    }

    private static ModeParse ParseMode(IReadOnlyList<string> args)
    {
        CliHelpMode mode = ModeFromEnvironment();
        var remaining = new List<string>();
        for (int i = 0; i < args.Count; i++)
        {
            string token = args[i];
            if (TokenComparer.Equals(token, "--plain-help") || TokenComparer.Equals(token, "--no-ansi-help"))
            {
                mode = CliHelpMode.Plain;
                continue;
            }

            if (token.StartsWith("--help-mode=", StringComparison.OrdinalIgnoreCase))
            {
                mode = ParseModeValue(token["--help-mode=".Length..]);
                continue;
            }

            if (TokenComparer.Equals(token, "--help-mode") && i + 1 < args.Count)
            {
                mode = ParseModeValue(args[++i]);
                continue;
            }

            remaining.Add(token);
        }

        return new ModeParse(mode, remaining);
    }

    private static CliHelpMode ModeFromEnvironment()
    {
        string? noColor = Environment.GetEnvironmentVariable("NO_COLOR");
        if (!string.IsNullOrEmpty(noColor))
        {
            return CliHelpMode.Plain;
        }

        string? mode = Environment.GetEnvironmentVariable("HX_HELP_MODE");
        return string.IsNullOrWhiteSpace(mode) ? CliHelpMode.Auto : ParseModeValue(mode);
    }

    private static CliHelpMode ParseModeValue(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "plain" or "vanilla" or "text" => CliHelpMode.Plain,
            "rich" or "ansi" or "color" => CliHelpMode.Rich,
            _ => CliHelpMode.Auto,
        };

    private static int IndexOfHelp(IReadOnlyList<string> tokens)
    {
        for (int i = 0; i < tokens.Count; i++)
        {
            if (tokens[i] is "-h" or "--help" or "-?" or "help")
            {
                return i;
            }
        }

        return -1;
    }

    private static List<string> PathTokens(IReadOnlyList<string> tokens, int helpIndex)
    {
        if (TokenComparer.Equals(tokens[helpIndex], "help"))
        {
            return tokens.Skip(helpIndex + 1).Where(IsCommandToken).ToList();
        }

        return tokens.Take(helpIndex).Where(IsCommandToken).ToList();
    }

    private static bool IsCommandToken(string token) => !token.StartsWith("-", StringComparison.Ordinal);

    private static Command Resolve(RootCommand root, IReadOnlyList<string> pathTokens)
    {
        Command command = root;
        foreach (string token in pathTokens)
        {
            Command? next = command.Subcommands.FirstOrDefault(c => TokenComparer.Equals(c.Name, token));
            if (next is null)
            {
                break;
            }

            command = next;
        }

        return command;
    }

    private static IReadOnlyList<string> PathFrom(RootCommand root, Command command)
    {
        if (ReferenceEquals(root, command))
        {
            return [];
        }

        var path = new List<string>();
        if (TryFind(root, command, path))
        {
            return path;
        }

        return [command.Name];
    }

    private static bool TryFind(Command current, Command target, List<string> path)
    {
        foreach (Command child in current.Subcommands)
        {
            path.Add(child.Name);
            if (ReferenceEquals(child, target) || TryFind(child, target, path))
            {
                return true;
            }

            path.RemoveAt(path.Count - 1);
        }

        return false;
    }

    private sealed record ModeParse(CliHelpMode Mode, IReadOnlyList<string> Remaining);
}
