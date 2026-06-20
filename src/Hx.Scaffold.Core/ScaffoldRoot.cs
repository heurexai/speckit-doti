namespace Hx.Scaffold.Core;

/// <summary>Locates the scaffold repo root (the directory containing <c>scaffold-dotnet.slnx</c>),
/// which is the SOURCE of the template pack, vendored tools, and Doti assets.</summary>
public static class ScaffoldRoot
{
    public const string Marker = "scaffold-dotnet.slnx";

    public static string Find(string startDirectory)
    {
        DirectoryInfo? dir = new(Path.GetFullPath(startDirectory));
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, Marker)))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate the scaffold root ('{Marker}') from '{startDirectory}'. Run from inside the scaffold repo.");
    }
}
