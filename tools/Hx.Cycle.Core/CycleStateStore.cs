using System.Text.Json;
using Hx.Tooling.Contracts;

namespace Hx.Cycle.Core;

/// <summary>
/// Reads/writes the persistent cycle state at <c>.doti/cycle-state.json</c> (gitignored local working
/// state). Indented JSON via the shared contract options, so a human can inspect the proofs.
/// </summary>
public sealed class CycleStateStore
{
    public const string RelativePath = ".doti/cycle-state.json";

    private readonly string _path;

    public CycleStateStore(string repositoryRoot) =>
        _path = Path.GetFullPath(Path.Combine(repositoryRoot, ".doti", "cycle-state.json"));

    public bool Exists => File.Exists(_path);

    public CycleState? Read()
    {
        if (!File.Exists(_path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<CycleState>(File.ReadAllText(_path), JsonContractSerializerOptions.Create());
    }

    public void Write(CycleState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        JsonSerializerOptions options = JsonContractSerializerOptions.Create();
        options.WriteIndented = true;
        string temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(state, options));
        File.Move(temp, _path, overwrite: true);
    }
}
