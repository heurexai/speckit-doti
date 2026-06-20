using Xunit;

namespace Hx.Runner.Tests;

public sealed class LineEndingPolicyTests
{
    [Fact]
    public void GitAttributesPinsTextFormatsToLf()
    {
        string repositoryRoot = FindRepositoryRoot();
        string gitAttributes = File.ReadAllText(Path.Combine(repositoryRoot, ".gitattributes"));

        Assert.Contains("*.cs text eol=lf", gitAttributes, StringComparison.Ordinal);
        Assert.Contains("*.csproj text eol=lf", gitAttributes, StringComparison.Ordinal);
        Assert.Contains("*.json text eol=lf", gitAttributes, StringComparison.Ordinal);
        Assert.Contains("*.md text eol=lf", gitAttributes, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, ".gitattributes")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }
}
