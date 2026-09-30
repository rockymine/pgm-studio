using System.Text.RegularExpressions;

namespace PgmStudio.Api.Tests;

/// <summary>
/// <see cref="PgmStudio.Api.Services.FeatureData"/> is the one reader of a map's scan, because a finished
/// sketch's scan is a copy of its layout and only that reader brings the copy up to date first. A route that
/// loads the rows or the artifacts itself answers for the board Finish saw, and nothing fails when it does —
/// so the source is held to it.
/// </summary>
public sealed partial class FeatureDataTests
{
    /// <summary>The files allowed to touch the stored scan directly: the accessor, the two helpers it reads
    /// through, and the refresh that writes it.</summary>
    private static readonly string[] Readers =
        ["Services/FeatureData.cs", "Services/MapBounds.cs", "Services/SketchFinish.cs", "Endpoints/ConfigureEndpoints.cs"];

    [GeneratedRegex(@"db\.(Segments|FloorMarks|DoorRuns)\b|LoadAsync\([^;]*ArtifactKind\.(SurfaceParquet|IslandsJson|MapConfigJson)|ScanConfig\.LoadAsync|MapBounds\.ResolveAsync")]
    private static partial Regex ScanRead();

    [Test]
    public async Task No_route_reads_the_scan_around_the_accessor()
    {
        var api = ApiRoot();
        var offenders = Directory.EnumerateFiles(api, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                           && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Select(path => Path.GetRelativePath(api, path).Replace(Path.DirectorySeparatorChar, '/'))
            .Where(relative => !Readers.Contains(relative))
            .SelectMany(relative => File.ReadLines(Path.Combine(api, relative))
                .Select((line, index) => (relative, line, index))
                .Where(entry => ScanRead().IsMatch(entry.line))
                .Select(entry => $"{entry.relative}:{entry.index + 1}"))
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("a scan read outside FeatureData skips the refresh: " + string.Join(", ", offenders));
    }

    private static string ApiRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "PgmStudio.Api")))
            dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException(
            "no src/PgmStudio.Api above the test output — the repository layout moved"), "src", "PgmStudio.Api");
    }
}
