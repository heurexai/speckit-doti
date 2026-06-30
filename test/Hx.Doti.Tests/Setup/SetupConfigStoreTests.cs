using Hx.Doti.Core.Setup;
using Hx.Tooling.Contracts.Setup;
using Xunit;

namespace Hx.Doti.Tests.Setup;

/// <summary>029 T006 (FR-003/D6): the .doti/setup.json store persists repo-portable intent only — a machine-local
/// localOutput.directory never lands in the tracked file; reads are fail-soft; round-trips.</summary>
public sealed class SetupConfigStoreTests
{
    [Fact]
    public void Machine_local_directory_is_never_persisted()
    {
        string repo = NewRepo();
        try
        {
            var intent = new SetupConfig
            {
                Identity = new SetupIdentityConfig { License = "Apache-2.0" },
                Release = new SetupReleaseConfig { EnvironmentVariable = "ACME_ROOT", Directory = "/abs/machine/path", Enabled = false },
            };

            string? path = SetupConfigStore.Write(repo, intent);
            Assert.NotNull(path);

            string json = File.ReadAllText(Path.Combine(repo, ".doti", "setup.json"));
            Assert.DoesNotContain("/abs/machine/path", json);   // D6: machine-local directory stripped
            Assert.DoesNotContain("\"directory\"", json);
            Assert.DoesNotContain("\"enabled\"", json);          // machine-local enabled stripped
            Assert.Contains("ACME_ROOT", json);                  // the portable env-var name is kept
            Assert.Contains("Apache-2.0", json);
        }
        finally
        {
            ForceDelete(repo);
        }
    }

    [Fact]
    public void Round_trips_portable_intent()
    {
        string repo = NewRepo();
        try
        {
            var intent = new SetupConfig
            {
                SchemaVersion = 1,
                Identity = new SetupIdentityConfig { Name = "Acme.Widget", License = "MIT" },
                Versioning = new SetupVersioningConfig { NextVersion = "1.0.0" },
                Agents = ["claude"],
            };
            SetupConfigStore.Write(repo, intent);

            SetupConfig? read = SetupConfigStore.Read(repo);
            Assert.NotNull(read);
            Assert.Equal("Acme.Widget", read!.Identity!.Name);
            Assert.Equal("1.0.0", read.Versioning!.NextVersion);
            Assert.Equal(["claude"], read.Agents!);
        }
        finally
        {
            ForceDelete(repo);
        }
    }

    [Fact]
    public void Write_from_resolved_reconstructs_intent_from_non_default_fields()
    {
        string repo = NewRepo();
        try
        {
            var config = new SetupConfig
            {
                Identity = new SetupIdentityConfig { Company = "Acme" }, // authors derives from company (also intent)
                Versioning = new SetupVersioningConfig { NextVersion = "3.0.0" },
            };
            ResolvedSetupConfig resolved = SetupConfigResolver.Resolve(config, null, SetupAudience.New);

            string? path = SetupConfigStore.WriteFromResolved(repo, resolved);
            Assert.NotNull(path);

            SetupConfig? read = SetupConfigStore.Read(repo);
            Assert.Equal("Acme", read!.Identity!.Company);
            Assert.Equal("3.0.0", read.Versioning!.NextVersion);
            // license stayed default → not persisted (only operator intent is recorded).
            Assert.Null(read.Identity.License);
        }
        finally
        {
            ForceDelete(repo);
        }
    }

    [Fact]
    public void No_portable_intent_writes_nothing()
    {
        string repo = NewRepo();
        try
        {
            // An all-default resolved config carries no custom field → no setup.json (no spurious all-default file).
            ResolvedSetupConfig resolved = SetupConfigResolver.Resolve(new SetupConfig(), null, SetupAudience.New);
            Assert.Null(SetupConfigStore.WriteFromResolved(repo, resolved));
            Assert.False(File.Exists(Path.Combine(repo, ".doti", "setup.json")));
        }
        finally
        {
            ForceDelete(repo);
        }
    }

    [Fact]
    public void Missing_and_malformed_read_as_null()
    {
        string repo = NewRepo();
        try
        {
            Assert.Null(SetupConfigStore.Read(repo));
            Directory.CreateDirectory(Path.Combine(repo, ".doti"));
            File.WriteAllText(Path.Combine(repo, ".doti", "setup.json"), "{ not json ");
            Assert.Null(SetupConfigStore.Read(repo));
        }
        finally
        {
            ForceDelete(repo);
        }
    }

    private static string NewRepo()
    {
        string dir = Path.Combine(Path.GetTempPath(), "doti-setup-store-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void ForceDelete(string dir)
    {
        try { Directory.Delete(dir, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
