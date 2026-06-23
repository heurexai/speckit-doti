using System.CommandLine;
using System.Diagnostics;
using Hx.Tooling.Contracts;
using Spectre.Console;

namespace Hx.Cli.Kernel;

/// <summary>
/// Heurex-branded human/TTY rendering (Spectre.Console): the ANSI-art banner + table help, and a live
/// progress display for long-running commands with a final summary panel. Spectre lives here in the kernel —
/// the single place human output is produced — so the agent-first JSON envelope path (<see cref="CliWriter"/>)
/// never touches it and stays byte-stable. Help rendering takes an injectable <see cref="IAnsiConsole"/>
/// (default the real console) so it is testable; the live-progress path is the interactive TTY only.
/// </summary>
public static class CliRenderer
{
    // Heurex palette (brand-guidelines.yaml): primary navy #1A1F4D, secondary gold #C9A961, light #F4F6F9.
    private static readonly Color Gold = new(201, 169, 97);
    private static readonly Color Navy = new(26, 31, 77);
    private static readonly Color Fail = new(200, 80, 80);
    private const string GoldHex = "#C9A961";
    private const string LightHex = "#F4F6F9";
    private const string MutedHex = "#8B91A7";

    /// <summary>The branded banner (figlet ANSI art) + command/option tables — the human help screen.</summary>
    public static void WriteHelp(
        RootCommand root,
        Command command,
        IReadOnlyList<string> path,
        CliMeta meta,
        string banner,
        string tagline,
        CliHelpMode mode,
        IAnsiConsole? console = null,
        TextWriter? plainOutput = null)
    {
        if (ShouldWritePlain(mode))
        {
            (plainOutput ?? Console.Out).Write(RenderPlainHelp(root, command, path, meta, banner, tagline));
            return;
        }

        WriteRichHelp(root, command, path, meta, banner, tagline, mode, console);
    }

    public static string RenderPlainHelp(
        RootCommand root,
        Command command,
        IReadOnlyList<string> path,
        CliMeta meta,
        string banner,
        string tagline)
    {
        var lines = new List<string>
        {
            $"{banner} {meta.Version}",
            tagline,
            string.Empty,
            $"{Invocation(meta, path)} - {Description(command)}",
            string.Empty,
            "Usage:",
            $"  {Invocation(meta, path)}{UsageSuffix(command)}",
            string.Empty,
        };

        if (command.Subcommands.Count > 0)
        {
            lines.Add("Commands:");
            AddPlainRows(lines, command.Subcommands.Select(c => (c.Name, Description(c))));
            lines.Add(string.Empty);
        }

        lines.Add("Options:");
        AddPlainRows(lines, HelpOptions(command));
        lines.Add(string.Empty);
        return string.Join(Environment.NewLine, lines);
    }

    private static void WriteRichHelp(
        RootCommand root,
        Command command,
        IReadOnlyList<string> path,
        CliMeta meta,
        string banner,
        string tagline,
        CliHelpMode mode,
        IAnsiConsole? console = null)
    {
        IAnsiConsole c = ResolveConsole(mode, console);
        c.WriteLine();
        c.Write(new FigletText(banner).LeftJustified().Color(Gold));
        c.MarkupLine($"  [{MutedHex}]{Markup.Escape(tagline)}[/]   [{GoldHex}]{Markup.Escape(meta.Version)}[/]");
        c.WriteLine();
        c.MarkupLine($"  [{GoldHex}]{Markup.Escape(Invocation(meta, path))}[/] [{MutedHex}]- {Markup.Escape(Description(command))}[/]");
        c.WriteLine();

        string title = $"{Invocation(meta, path)}  -  {Description(command)}";
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Navy)
            .Title(Markup.Escape(title), new Style(Gold))
            .Expand();
        table.AddColumn(new TableColumn($"[{GoldHex}]Command[/]"));
        table.AddColumn(new TableColumn($"[{GoldHex}]Description[/]"));
        foreach (Command sub in command.Subcommands)
        {
            table.AddRow($"[{LightHex}]{Markup.Escape(sub.Name)}[/]", Markup.Escape(Description(sub)));
        }

        if (command.Subcommands.Count > 0)
        {
            c.Write(table);
            c.WriteLine();
        }

        var options = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Navy)
            .Title("Options", new Style(Gold))
            .Expand();
        options.AddColumn(new TableColumn($"[{GoldHex}]Option[/]"));
        options.AddColumn(new TableColumn($"[{GoldHex}]Description[/]"));
        foreach ((string name, string description) in HelpOptions(command))
        {
            options.AddRow($"[{LightHex}]{Markup.Escape(name)}[/]", Markup.Escape(description));
        }

        c.MarkupLine($"  [{MutedHex}]usage:[/] [{LightHex}]{Markup.Escape(Invocation(meta, path) + UsageSuffix(command))}[/]");
        c.WriteLine();
        c.Write(options);
        c.WriteLine();
    }

    private static bool ShouldWritePlain(CliHelpMode mode) =>
        mode == CliHelpMode.Plain || mode == CliHelpMode.Auto && Console.IsOutputRedirected;

    private static IAnsiConsole ResolveConsole(CliHelpMode mode, IAnsiConsole? console) =>
        console ?? (mode == CliHelpMode.Rich
            ? AnsiConsole.Create(new AnsiConsoleSettings
            {
                Ansi = AnsiSupport.Yes,
                ColorSystem = ColorSystemSupport.TrueColor,
                Out = new AnsiConsoleOutput(Console.Out),
            })
            : AnsiConsole.Console);

    private static string Invocation(CliMeta meta, IReadOnlyList<string> path) =>
        path.Count == 0 ? meta.Tool : meta.Tool + " " + string.Join(' ', path);

    private static string UsageSuffix(Command command)
    {
        bool hasCommands = command.Subcommands.Count > 0;
        return hasCommands ? " [command] [options]" : " [options]";
    }

    private static string Description(Command command) =>
        string.IsNullOrWhiteSpace(command.Description) ? "No description." : command.Description!;

    private static IReadOnlyList<(string Name, string Description)> HelpOptions(Command command)
    {
        var rows = command.Options
            .Select(o => (o.Name, string.IsNullOrWhiteSpace(o.Description) ? "No description." : o.Description!))
            .ToList();
        rows.Add(("--help-mode <auto|rich|plain>", "Select human help rendering. Also honors HX_HELP_MODE and NO_COLOR."));
        rows.Add(("--plain-help", "Shortcut for --help-mode plain."));
        rows.Add(("-h, --help, -?", "Show help for this command."));
        return rows;
    }

    private static void AddPlainRows(List<string> lines, IEnumerable<(string Name, string Description)> rows)
    {
        (string Name, string Description)[] materialized = rows.ToArray();
        int width = materialized.Length == 0 ? 0 : materialized.Max(r => r.Name.Length);
        foreach ((string name, string description) in materialized)
        {
            lines.Add($"  {name.PadRight(width)}  {description}");
        }
    }

    /// <summary>
    /// Run <paramref name="body"/> behind a live progress display — one gold bar per emitted <see cref="CliEvent"/>
    /// step (added dynamically as steps run) — then render a summary panel. Interactive TTY only; the JSON path
    /// stays in <see cref="CliHost"/>.
    /// </summary>
    public static int RunWithLiveProgress(CliMeta meta, string command, Func<Action<CliEvent>, CliResult> body)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[{GoldHex}]{Markup.Escape(meta.Tool)}[/] [{LightHex}]{Markup.Escape(command)}[/]");

        var stopwatch = Stopwatch.StartNew();
        CliResult result = null!;
        var tasks = new Dictionary<string, ProgressTask>(StringComparer.Ordinal);

        AnsiConsole.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
                new SpinnerColumn(Spinner.Known.Dots) { Style = new Style(Gold) },
                new TaskDescriptionColumn { Alignment = Justify.Left },
                new ProgressBarColumn
                {
                    CompletedStyle = new Style(Gold), FinishedStyle = new Style(Gold),
                    RemainingStyle = new Style(Navy), IndeterminateStyle = new Style(Gold),
                },
                new PercentageColumn { Style = new Style(Color.Grey), CompletedStyle = new Style(Gold) })
            .Start(ctx =>
            {
                void OnEvent(CliEvent e)
                {
                    if (!string.Equals(e.Event, "step", StringComparison.Ordinal))
                    {
                        return;
                    }

                    if (!tasks.TryGetValue(e.Name, out ProgressTask? task))
                    {
                        task = ctx.AddTask($"[{LightHex}]{Markup.Escape(Label(e.Name))}[/]",
                            new ProgressTaskSettings { AutoStart = false });
                        tasks[e.Name] = task;
                    }

                    if (e.Status == "running")
                    {
                        if (!task.IsStarted)
                        {
                            task.StartTask();
                        }

                        task.IsIndeterminate = true;
                    }
                    else
                    {
                        Complete(task);
                    }
                }

                try
                {
                    result = body(OnEvent);
                }
                catch (Exception ex)
                {
                    result = CliResults.Fail(meta, command, ExitClass.Internal,
                        [Diag.Of(ErrorCodes.Internal_Unhandled, $"{command} failed: {ex.Message}")]);
                }

                foreach (ProgressTask task in tasks.Values)
                {
                    if (task.IsStarted && !task.IsFinished)
                    {
                        Complete(task);
                    }
                }
            });

        result = result with { ElapsedMs = stopwatch.ElapsedMilliseconds };
        WriteSummary(AnsiConsole.Console, result);
        return result.ExitCode;
    }

    private static void Complete(ProgressTask task)
    {
        task.IsIndeterminate = false;
        task.Value = task.MaxValue;
        if (task.IsStarted && !task.IsFinished)
        {
            task.StopTask();
        }
    }

    private static void WriteSummary(IAnsiConsole c, CliResult result)
    {
        var lines = new List<string>
        {
            $"[{(result.Ok ? GoldHex : "red")}]{Markup.Escape(result.Summary)}[/]",
        };
        foreach (Diagnostic d in result.Errors)
        {
            lines.Add($"[red]{Markup.Escape(d.Severity.ToString().ToLowerInvariant())} {Markup.Escape(d.Code)}[/] " +
                      $"[{LightHex}]{Markup.Escape(d.Message)}[/]");
        }

        foreach (CliNextAction a in result.NextActions)
        {
            lines.Add(a.Command is { } cmd
                ? $"[{MutedHex}]next:[/] {Markup.Escape(a.Label)}  [{MutedHex}]({Markup.Escape(cmd)})[/]"
                : $"[{MutedHex}]next:[/] {Markup.Escape(a.Label)}");
        }

        c.WriteLine();
        c.Write(new Panel(new Markup(string.Join("\n", lines)))
            .Border(BoxBorder.Rounded)
            .BorderColor(result.Ok ? Gold : Fail)
            .Padding(1, 0, 1, 0));
        c.WriteLine();
    }

    private static string Label(string stepName) => stepName.Replace('-', ' ');
}
