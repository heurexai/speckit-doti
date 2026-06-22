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

    /// <summary>The branded banner (figlet ANSI art) + a table of the tool's commands — the human help screen.</summary>
    public static void WriteHelp(RootCommand root, CliMeta meta, string banner, string tagline, IAnsiConsole? console = null)
    {
        IAnsiConsole c = console ?? AnsiConsole.Console;
        c.WriteLine();
        c.Write(new FigletText(banner).LeftJustified().Color(Gold));
        c.MarkupLine($"  [{MutedHex}]{Markup.Escape(tagline)}[/]   [{GoldHex}]{Markup.Escape(meta.Version)}[/]");
        c.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Navy)
            .Title($"{meta.Tool}  —  {root.Description}", new Style(Gold))
            .Expand();
        table.AddColumn(new TableColumn($"[{GoldHex}]Command[/]"));
        table.AddColumn(new TableColumn($"[{GoldHex}]Description[/]"));
        foreach (Command sub in root.Subcommands)
        {
            table.AddRow($"[{LightHex}]{Markup.Escape(sub.Name)}[/]", Markup.Escape(sub.Description ?? string.Empty));
        }

        c.Write(table);
        c.MarkupLine($"  [{MutedHex}]options:[/] [{LightHex}]--json[/] [{MutedHex}](machine-readable envelope)[/]   [{LightHex}]-h, --help[/]");
        c.WriteLine();
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
