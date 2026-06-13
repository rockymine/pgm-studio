namespace PgmStudio.Api.Services;

/// <summary>Corpus roots under which a map's Minecraft world lives at <c>&lt;root&gt;/&lt;slug&gt;/region</c>.</summary>
public sealed class MapsRoots(IReadOnlyList<string> roots)
{
    public IReadOnlyList<string> Roots { get; } = roots;

    /// <summary>First existing <c>&lt;root&gt;/&lt;slug&gt;/region</c> directory, or null if none is present.</summary>
    public string? RegionDir(string slug) =>
        Roots.Select(r => Path.Combine(r, slug, "region")).FirstOrDefault(Directory.Exists);
}
