using System.Text;
using PgmStudio.Api.Endpoints;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Export;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Api.Services;

using Dict = Dictionary<string, object?>;

/// <summary>
/// The DB-touching prologue shared by <c>GET /map/{slug}/xml</c> and <c>GET /map/{slug}/export</c>. It loads
/// exactly what <see cref="MapExportComposer.Compose"/> needs from the store — whether the map is
/// intent-authored, the traversability segments, the stored authoring intent for a sketch map, the cached
/// surface palette and spawn-ore renewables for everything else, and the accounts behind the tree builders a
/// sketch names — and hands them to the pure composer, so the database and the network stay here and the
/// composition itself stays reachable by a driver that has neither.
/// </summary>
public static class MapExportLoader
{
    public static async Task<ExportComposition> ComposeAsync(
        long mapId, Dict doc, byte[]? layoutBytes, FeatureData feature, MapArtifactStore artifacts,
        PlayerLookup players, CancellationToken ct)
    {
        // A map is intent-authored iff it holds an intent — corpus maps have none, which is what scopes
        // the traversability judgement below to maps the studio itself authored.
        var isIntent = await artifacts.HasAsync(mapId, ArtifactKind.MapIntentJson, ct);

        var segments = isIntent ? await feature.SegmentsAsync(mapId, ct) : null;

        // Only a sketch-originated map resolves the stored intent against the world it builds.
        MapIntent? intent = layoutBytes is not null ? await artifacts.LoadJsonOrEmptyAsync<MapIntent>(mapId, ArtifactKind.MapIntentJson, ct) : null;

        // Only a non-sketch intent map draws on the cached scanned surface + resources (cache-only, never
        // triggering a world scan on export).
        IReadOnlySet<int>? surfacePalette = null;
        IReadOnlyList<(string Type, int X, int Y, int Z)> resources = [];
        if (isIntent && layoutBytes is null)
        {
            var surface = await ConfigureLayers.CellsAsync(artifacts, mapId, "surface", ct);
            surfacePalette = surface?.Select(c => c.BlockId).ToHashSet();
            resources = (await feature.ResourceBlocksAsync(mapId, ct)).Select(b => (b.Type, b.X, b.Y, b.Z)).ToList();
        }

        var accounts = layoutBytes is null ? null : await AccountsAsync(Encoding.UTF8.GetString(layoutBytes), players, ct);
        return MapExportComposer.Compose(doc, layoutBytes, isIntent, segments, intent, surfacePalette, resources, accounts);
    }

    /// <summary>The account behind each tree builder the layout names (<see cref="StudioCredits.Named"/>, which
    /// bounds how many), where one answers. A dressing document that does not parse names nobody here; the
    /// composer refuses it by name.</summary>
    private static async Task<IReadOnlyDictionary<string, (string Uuid, string Name)>> AccountsAsync(
        string layoutJson, PlayerLookup players, CancellationToken ct)
    {
        IReadOnlyList<string> named;
        try { named = StudioCredits.Named(layoutJson); }
        catch (DressingParseException) { named = []; }

        var accounts = new Dictionary<string, (string Uuid, string Name)>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in named)
            if (await players.ResolveAsync(name, ct) is { } account) accounts[name] = account;
        return accounts;
    }
}
