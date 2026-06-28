using System.CommandLine;
using System.Diagnostics;
using System.Text.Json;
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
        TextWriter? plainOutput = null,
        string? helpContext = null)
    {
        if (ShouldWritePlain(mode))
        {
            (plainOutput ?? Console.Out).Write(RenderPlainHelp(root, command, path, meta, banner, tagline, helpContext));
            return;
        }

        WriteRichHelp(root, command, path, meta, banner, tagline, mode, console, helpContext);
    }

    public static string RenderPlainHelp(
        RootCommand root,
        Command command,
        IReadOnlyList<string> path,
        CliMeta meta,
        string banner,
        string tagline,
        string? helpContext = null)
    {
        var lines = new List<string>
        {
            $"{banner} {meta.Version}",
            tagline,
        };
        if (!string.IsNullOrWhiteSpace(helpContext))
        {
            lines.Add(helpContext);
        }

        lines.Add(string.Empty);
        lines.Add($"{Invocation(meta, path)} - {Description(command)}");
        lines.Add(string.Empty);
        lines.Add("Usage:");
        lines.Add($"  {Invocation(meta, path)}{UsageSuffix(command)}");
        lines.Add(string.Empty);

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
        IAnsiConsole? console = null,
        string? helpContext = null)
    {
        IAnsiConsole c = ResolveConsole(mode, console);
        c.WriteLine();
        c.Write(new FigletText(banner).LeftJustified().Color(Gold));
        c.MarkupLine($"  [{MutedHex}]{Markup.Escape(tagline)}[/]   [{GoldHex}]{Markup.Escape(meta.Version)}[/]");
        if (!string.IsNullOrWhiteSpace(helpContext))
        {
            c.MarkupLine($"  [{MutedHex}]{Markup.Escape(helpContext)}[/]");
        }

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
                        // 012 FR-016: the streamed status IS the outcome — color the bar by it and surface the reason
                        // so a scope-skipped or failed step is visually distinct, not just "done". The reason is the
                        // step's first evidence message, carried on CliEvent.Message.
                        CompleteWithOutcome(task, e);
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

    // 012 FR-016: complete a step bar carrying its outcome — a skipped step is muted with its reason, a failed step
    // is red with its reason, a passed step keeps the gold bar. The reason (CliEvent.Message) is capped so the live
    // line never wraps into a dump.
    private static void CompleteWithOutcome(ProgressTask task, CliEvent e)
    {
        string label = Label(e.Name);
        string? reason = Cap(e.Message, 60);
        string description = e.Status switch
        {
            "skipped" => $"[{MutedHex}]{Markup.Escape(label)}[/] [{MutedHex}]skipped{ReasonSuffix(reason)}[/]",
            "fail" or "blocked" => $"[red]{Markup.Escape(label)}  fail{ReasonSuffix(reason)}[/]",
            _ => $"[{LightHex}]{Markup.Escape(label)}[/]",
        };
        task.Description = description;
        Complete(task);
    }

    private static string ReasonSuffix(string? reason) =>
        string.IsNullOrWhiteSpace(reason) ? string.Empty : $" — {Markup.Escape(reason)}";

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

        // 012 (FR-014/015/017): when the envelope carries a gate trace, render the operator-facing scope + change
        // summary + per-step ladder from that SAME trace the JSON exposes (one source of truth). Bounded, never a dump.
        if (GateTraceFrom(result) is { } trace)
        {
            WriteGateSummary(c, trace);
        }

        // 014 (FR-001/003/004): the standalone `architecture test`/`sentrux check` results carry the offender detail in
        // their Data (and so --json). Surface a brief, bounded human summary from that SAME data — one source of truth.
        WriteStandaloneStructuralSummary(c, result);
    }

    // 014 (FR-004/006): render a brief offender panel for the standalone structural commands from the result's Data.
    // Bounded/capped like the gate ladder; absent when the command is not a structural result or has no offenders.
    private static void WriteStandaloneStructuralSummary(IAnsiConsole c, CliResult result)
    {
        IReadOnlyList<string> offenders = StandaloneArchitectureOffenders(result);
        if (offenders.Count == 0)
        {
            offenders = StandaloneSentruxOffenders(result);
        }

        if (offenders.Count == 0)
        {
            return;
        }

        c.Write(new Panel(new Markup(string.Join("\n", offenders)))
            .Header($"[{GoldHex}]offenders[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Navy)
            .Padding(1, 0, 1, 0));
        c.WriteLine();
    }

    private static IReadOnlyList<string> StandaloneArchitectureOffenders(CliResult result)
    {
        ArchitectureTestResult? arch = DataAs<ArchitectureTestResult>(result);
        if (arch is null || arch.Outcome != StageOutcome.Fail)
        {
            return [];
        }

        ArchitectureViolation[] violations = arch.Tests
            .Where(test => test.Outcome == StageOutcome.Fail && test.Violations is { Count: > 0 })
            .SelectMany(test => test.Violations!)
            .OrderBy(v => v.Rule, StringComparer.Ordinal)
            .ThenBy(v => v.Description, StringComparer.Ordinal)
            .ToArray();
        return CapOffenders(violations.Select(FormatArchitectureOffender).ToArray());
    }

    private static IReadOnlyList<string> StandaloneSentruxOffenders(CliResult result)
    {
        SentruxCheckResult? sentrux = DataAs<SentruxCheckResult>(result);
        if (sentrux is null || sentrux.RulesOutcome != StageOutcome.Fail || sentrux.RuleViolationDetails is not { Count: > 0 } details)
        {
            return [];
        }

        SentruxViolation[] ordered = details
            .OrderBy(v => v.Rule, StringComparer.Ordinal)
            .ThenBy(v => v.File ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(v => v.Function ?? string.Empty, StringComparer.Ordinal)
            .ToArray();
        return CapOffenders(ordered.Select(FormatSentruxOffender).ToArray());
    }

    // Deserialize the result's Data ring as T (the same envelope the JSON carries). Any shape mismatch yields null.
    private static T? DataAs<T>(CliResult result) where T : class
    {
        if (result.Data is not { } data)
        {
            return null;
        }

        try
        {
            return data.Deserialize<T>(JsonContractSerializerOptions.Create());
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// 012 (FR-014/015/018/019): render the gate's effective-scope summary, the two-tier change summary, the
    /// per-step ladder (icon · name · duration · terse reason), and the total elapsed — all bounded/capped. Derived
    /// solely from the <see cref="GateTrace"/> the JSON carries, so the human surface matches the proof (FR-017).
    /// </summary>
    public static void WriteGateSummary(IAnsiConsole c, GateTrace trace)
    {
        var lines = new List<string> { ScopeLine(trace) };
        lines.Add(BasicChangeLine(trace.Change));
        if (trace.Change.ClassesIncluded || trace.Tests is not null)
        {
            lines.Add(DetailedChangeLine(trace));
        }

        lines.Add(string.Empty);
        foreach (GateStep step in trace.Steps)
        {
            lines.Add(LadderLine(step));
            // 014 (FR-004/006): under a FAILING structural step, render its offenders as concise one-line summaries,
            // deterministically ordered, capped with "+N more". A passing step shows no offender lines.
            lines.AddRange(StructuralOffenderLines(step, trace.StructuralViolations));
        }

        lines.Add(string.Empty);
        lines.Add($"[{MutedHex}]total:[/] [{LightHex}]{trace.TotalMs} ms[/]");

        c.Write(new Panel(new Markup(string.Join("\n", lines)))
            .Header($"[{GoldHex}]gate trace[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Navy)
            .Padding(1, 0, 1, 0));
        c.WriteLine();
    }

    private static string ScopeLine(GateTrace trace)
    {
        string scope = trace.Scope.DocsOnly ? "docs-only" : "code";
        string mode = trace.Scope.DocsOnly ? "no tests required" : $"affected-test mode: {trace.EffectiveMode}";
        return $"[{GoldHex}]scope:[/] [{LightHex}]{Markup.Escape(scope)}[/] [{MutedHex}]· {Markup.Escape(mode)}[/]";
    }

    private static string BasicChangeLine(ChangeSummary change)
    {
        string counts = $"src {change.Source} · test {change.Test} · docs {change.Docs} · other {change.Other}";
        string files = change.Files.Count == 0
            ? "no files"
            : string.Join(", ", change.Files.Select(Markup.Escape));
        return $"[{GoldHex}]changed:[/] [{LightHex}]{Markup.Escape(counts)}[/] " +
               $"[{MutedHex}](+{change.LinesAdded}/-{change.LinesRemoved})[/]\n" +
               $"[{MutedHex}]  files:[/] [{LightHex}]{files}[/]";
    }

    private static string DetailedChangeLine(GateTrace trace)
    {
        var parts = new List<string>();
        if (trace.Change.ClassesIncluded)
        {
            string classes = trace.Change.ClassesTouched.Count == 0
                ? "none"
                : string.Join(", ", trace.Change.ClassesTouched.Select(Markup.Escape));
            parts.Add($"[{MutedHex}]  classes:[/] [{LightHex}]{classes}[/]");
        }

        if (trace.Tests is { } tests)
        {
            parts.Add($"[{MutedHex}]  tests:[/] [{LightHex}]{tests.SelectedProjects}/{tests.TotalProjects} project(s)[/]"
                + CaseClassSuffix(tests));
        }

        return string.Join("\n", parts);
    }

    private static string CaseClassSuffix(AffectedTestInventory tests)
    {
        if (tests.SelectedClasses is { } classes && tests.SelectedCases is { } cases)
        {
            string total = tests.TotalClasses is { } tc && tests.TotalCases is { } tcase
                ? $" of {tc} class(es)/{tcase} case(s)"
                : tests.UnknownReason is { } reason ? $" [{MutedHex}](total unknown: {Markup.Escape(Cap(reason, 50)!)})[/]" : string.Empty;
            return $" [{MutedHex}]· {classes} class(es)/{cases} case(s) selected{total}[/]";
        }

        return tests.UnknownReason is { } r ? $" [{MutedHex}](class/case counts unknown: {Markup.Escape(Cap(r, 50)!)})[/]" : string.Empty;
    }

    private static string LadderLine(GateStep step)
    {
        (string glyph, string word, string color) = step.Outcome switch
        {
            StageOutcome.Pass => ("ok", "pass", GoldHex),
            StageOutcome.Skipped => ("--", "skipped", MutedHex),
            _ => ("XX", "fail", "red"),
        };
        string duration = step.DurationMs is { } ms ? $" [{MutedHex}]{ms} ms[/]" : string.Empty;
        string reason = step.Outcome is StageOutcome.Skipped or StageOutcome.Fail or StageOutcome.Blocked
            ? ReasonSuffix(Cap(step.Evidence.FirstOrDefault()?.Message, 70))
            : string.Empty;
        return $"[{color}]{glyph}[/] [{LightHex}]{Markup.Escape(Label(step.Name))}[/] " +
               $"[{color}]{word}[/]{duration}{reason}";
    }

    // 014 (FR-004/006): up to this many offender lines per failing structural step, then an explicit "+N more". The
    // full offender set always remains in --json (the trace the JSON carries is uncapped).
    private const int OffenderCap = 5;

    // 014 (FR-004): for a FAILING architecture-test/sentrux-* step, render the matching trace offenders as concise,
    // deterministically ordered one-line summaries. Empty for a passing step or a step with no captured offenders.
    private static IReadOnlyList<string> StructuralOffenderLines(
        GateStep step, IReadOnlyList<StructuralStepViolations>? structural)
    {
        bool failing = step.Outcome is StageOutcome.Fail or StageOutcome.Blocked;
        if (!failing || structural is null || !IsStructuralStep(step.Name))
        {
            return [];
        }

        StructuralStepViolations? match = structural.FirstOrDefault(s =>
            string.Equals(s.StepName, step.Name, StringComparison.Ordinal));
        if (match is null)
        {
            return [];
        }

        IReadOnlyList<string> formatted = match.Architecture.Count > 0
            ? match.Architecture.Select(FormatArchitectureOffender).ToArray()
            : match.Sentrux.Select(FormatSentruxOffender).ToArray();
        return CapOffenders(formatted);
    }

    private static bool IsStructuralStep(string stepName) =>
        string.Equals(stepName, "architecture-test", StringComparison.Ordinal)
        || stepName.StartsWith("sentrux-", StringComparison.Ordinal);

    private static IReadOnlyList<string> CapOffenders(IReadOnlyList<string> offenders)
    {
        if (offenders.Count <= OffenderCap)
        {
            return [.. offenders.Select(o => $"[{MutedHex}]    ↳[/] {o}")];
        }

        var capped = offenders.Take(OffenderCap)
            .Select(o => $"[{MutedHex}]    ↳[/] {o}")
            .ToList();
        capped.Add($"[{MutedHex}]    ↳ +{offenders.Count - OffenderCap} more[/]");
        return capped;
    }

    // cliSurfaceConfinement: FooService, BarService +N more — name the rule + the violating objects (capped). When the
    // detail could not be recovered, surface the unknown reason instead of a fabricated object list (FR-005).
    private static string FormatArchitectureOffender(ArchitectureViolation violation)
    {
        if (violation.UnknownReason is { } reason)
        {
            return $"[{LightHex}]{Markup.Escape(violation.Rule)}[/] [{MutedHex}](detail unknown — {Markup.Escape(Cap(reason, 60)!)})[/]";
        }

        string objects = violation.ViolatingObjects.Count == 0
            ? "(no objects reported)"
            : JoinCapped(violation.ViolatingObjects.Select(ShortTypeName).ToArray());
        return $"[{LightHex}]{Markup.Escape(violation.Rule)}:[/] {objects}";
    }

    // Cap a single rule's object list inline (the example: "FooService, BarService +N more"); the full set is in JSON.
    private static string JoinCapped(IReadOnlyList<string> values)
    {
        if (values.Count <= OffenderCap)
        {
            return Join(values);
        }

        return Join(values.Take(OffenderCap)) + $" [{MutedHex}]+{values.Count - OffenderCap} more[/]";
    }

    // max_cc: ProcessFoo() — Bar.cs:42 (CC 28 > 25) — use the fields present; if the location is unknown, surface the
    // rule + message + the reason rather than a fabricated location (FR-005).
    private static string FormatSentruxOffender(SentruxViolation violation)
    {
        if (violation.UnknownReason is { } reason)
        {
            string message = violation.Message is { } m ? Cap(m, 50)! : "(no message)";
            return $"[{LightHex}]{Markup.Escape(violation.Rule)}:[/] {Markup.Escape(message)} " +
                   $"[{MutedHex}](location unknown — {Markup.Escape(Cap(reason, 60)!)})[/]";
        }

        string function = violation.Function is { } fn ? $"{Markup.Escape(fn)}() " : string.Empty;
        string location = violation.File is { } file
            ? $"— {Markup.Escape(ShortPath(file))}{(violation.Line is { } line ? $":{line}" : string.Empty)}"
            : string.Empty;
        string measure = violation.MeasuredValue is { } value && violation.Limit is { } limit
            ? $" [{MutedHex}]({Markup.Escape(value)} > {Markup.Escape(limit)})[/]"
            : string.Empty;
        string body = $"{function}{location}".Trim();
        return $"[{LightHex}]{Markup.Escape(violation.Rule)}:[/] {body}{measure}";
    }

    private static string Join(IEnumerable<string> values) =>
        string.Join(", ", values.Select(Markup.Escape));

    // Render the leaf type name (the last dotted segment) so a long FQN does not blow the one-line budget.
    private static string ShortTypeName(string fullName)
    {
        int dot = fullName.LastIndexOf('.');
        return dot >= 0 && dot < fullName.Length - 1 ? fullName[(dot + 1)..] : fullName;
    }

    // Render the file name (the last path segment) so the offender line stays scannable.
    private static string ShortPath(string path)
    {
        int slash = path.LastIndexOfAny(['/', '\\']);
        return slash >= 0 && slash < path.Length - 1 ? path[(slash + 1)..] : path;
    }

    // Deserialize the trace from the envelope's JsonNode data — one source of truth with the JSON, no separate
    // human-only computation (FR-017). Any shape mismatch yields null (the panel-only render).
    private static GateTrace? GateTraceFrom(CliResult result)
    {
        if (result.Data is not { } data)
        {
            return null;
        }

        try
        {
            GateRunResult? run = data.Deserialize<GateRunResult>(JsonContractSerializerOptions.Create());
            return run?.Trace;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static string? Cap(string? text, int max)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        string single = text.ReplaceLineEndings(" ").Trim();
        return single.Length <= max ? single : single[..(max - 1)] + "…";
    }

    private static string Label(string stepName) => stepName.Replace('-', ' ');
}
