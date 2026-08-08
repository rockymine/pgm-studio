namespace PgmStudio.Api.Services;

/// <summary>Corpus roots under which a map's Minecraft world lives at <c>&lt;root&gt;/&lt;slug&gt;/region</c>.</summary>
public sealed class MapsRoots(IReadOnlyList<string> roots)
{
    public IReadOnlyList<string> Roots { get; } = roots;

    /// <summary>First existing <c>&lt;root&gt;/&lt;slug&gt;/region</c> directory, or null if none is present.</summary>
    public string? RegionDir(string slug) =>
        Roots.Select(r => Path.Combine(r, slug, "region")).FirstOrDefault(Directory.Exists);

    /// <summary>First existing <c>&lt;root&gt;/&lt;slug&gt;/map.xml</c>, or null. Only a corpus map has one:
    /// a world arriving through the zip or folder import carries no XML by design — the XML is what the
    /// configure tool produces from it, not an input — so this is null on the product's own import path.</summary>
    public string? MapXmlPath(string slug) =>
        Roots.Select(r => Path.Combine(r, slug, "map.xml")).FirstOrDefault(File.Exists);
}
