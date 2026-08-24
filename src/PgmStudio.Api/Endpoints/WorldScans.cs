using PgmStudio.Contracts;
using PgmStudio.Data.Features;

namespace PgmStudio.Api.Endpoints;

/// <summary>The one place a world scan's answer is built, so the three routes that run one — re-scan, import
/// a folder, import an archive — cannot report the same counts differently. Each adds only what its own way
/// of reaching the world lets it say.</summary>
internal static class WorldScans
{
    public static WorldScanDto Of(string slug, WorldFeatureWriter.Counts counts) =>
        new(slug, counts.WoolBlocks, counts.ResourceBlocks, counts.ChestItems, counts.SpawnerBlocks,
            counts.ScanSegments, counts.Islands, counts.MonumentCandidates, counts.CoreCandidates);
}
