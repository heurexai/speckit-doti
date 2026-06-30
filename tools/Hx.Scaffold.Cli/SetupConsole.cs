namespace Hx.Scaffold.Cli;

/// <summary>029 D4: the injectable console seam for the setup wizard — IO behind an interface so the wizard loop runs
/// on scripted input with no TTY (SC-004 testable). Neutral name (no forbidden cliSurfaceConfinement suffix).</summary>
public interface ISetupConsole
{
    /// <summary>Print a line (a prompt, a heading, the "what it affects" hint, or the Review render).</summary>
    void WriteLine(string text);

    /// <summary>Read one answer line; <c>null</c> at end-of-input (treated as "accept the default").</summary>
    string? ReadLine();
}

/// <summary>029 D4: the real console implementation backing the wizard when no test seam is injected.</summary>
public sealed class SystemSetupConsole : ISetupConsole
{
    public static readonly SystemSetupConsole Instance = new();

    public void WriteLine(string text) => Console.Error.WriteLine(text);

    public string? ReadLine() => Console.In.ReadLine();
}

/// <summary>029 D4: a scripted console for tests — answers are dequeued in order; output is captured.</summary>
public sealed class ScriptedSetupConsole : ISetupConsole
{
    private readonly Queue<string> _answers;
    private readonly List<string> _output = [];

    public ScriptedSetupConsole(IEnumerable<string> answers) => _answers = new Queue<string>(answers);

    public IReadOnlyList<string> Output => _output;

    public void WriteLine(string text) => _output.Add(text);

    public string? ReadLine() => _answers.Count > 0 ? _answers.Dequeue() : null;
}
