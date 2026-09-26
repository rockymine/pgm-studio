namespace PgmStudio.E2e.Tests.Harness;

/// <summary>Where the app under test listens, and where a run writes what it produces.</summary>
public static class Studio
{
    /// <summary>The app under test — <c>E2E_BASE</c>, which <c>tools/e2e.sh</c> sets to its own port.</summary>
    public static readonly string Base =
        (Environment.GetEnvironmentVariable("E2E_BASE") is { Length: > 0 } named ? named : "http://localhost:7895")
        .TrimEnd('/');

    /// <summary>The repository root: the nearest directory above the test binary holding the solution file.</summary>
    public static readonly string RepoRoot = FindRepoRoot();

    /// <summary>The repository's <c>.tmp/</c>, where screenshots land; created on first use.</summary>
    public static string TmpDir
    {
        get
        {
            var directory = Path.Combine(RepoRoot, ".tmp");
            Directory.CreateDirectory(directory);
            return directory;
        }
    }

    /// <summary>The built API assembly, for a spec that starts a second server over the suite's database.</summary>
    public static string ApiDll =>
        Path.Combine(RepoRoot, "src", "PgmStudio.Api", "bin", "Debug", "net10.0", "PgmStudio.Api.dll");

    private static string FindRepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PgmStudio.slnx"))) return directory.FullName;
        }
        throw new InvalidOperationException($"no PgmStudio.slnx above {AppContext.BaseDirectory}");
    }
}
