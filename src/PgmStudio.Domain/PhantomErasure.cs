using PgmStudio.Geom;
namespace PgmStudio.Domain;

/// <summary>
/// The blocks a map deletes <b>before play begins</b>, stated by the map rather than guessed from materials.
///
/// <para>Authors reach for a hidden destroyable (<see cref="PhantomKind.BlockSwap"/>) to script the world,
/// and the commonest use is a build-floor marker: a sheet of blocks laid at the world floor so PGM's void
/// filter reads those columns as buildable, erased by a mode at <c>0s</c> so nobody ever sees it. Such a sheet
/// is not terrain — it is gone before the first tick — so anything describing the playable ground has to
/// subtract it, or two islands read as one because a floor bridges them.</para>
///
/// <para>A phantom names exactly what vanishes: its region says where and its <see cref="Destroyable.Materials"/>
/// say which blocks, so the subtraction is a fact per map rather than a material heuristic. The heuristic it
/// replaces excluded stained glass everywhere, which served the two corpus maps that use it and silently ate
/// every decorative glass floor in the rest.</para>
///
/// <para><b>Before play</b> is the whole test, and it is why the mode's timing is read. A mode at <c>0s</c>
/// erases its blocks during load; a mode at <c>15m</c> changes the world mid-match, so those blocks <i>are</i>
/// terrain when the match starts and must be kept. The opposite direction is the water lane — materials
/// <c>air</c> swapped to <c>water</c> — where blocks appear rather than vanish, and nothing is subtracted.</para>
///
/// <para>Both unknown cases fail closed, keeping the block as terrain: a material name with no id, and a
/// region shape that does not reduce to boxes. An erasure this cannot resolve leaves the ground exactly as it
/// reads without one.</para>
/// </summary>
public sealed class PhantomErasure
{
    private readonly List<(BlockBox Box, IReadOnlySet<int> Ids)> _volumes;

    private PhantomErasure(List<(BlockBox, IReadOnlySet<int>)> volumes) => _volumes = volumes;

    /// <summary>Nothing is erased — the state for the overwhelming majority of maps.</summary>
    public static readonly PhantomErasure None = new([]);

    public bool IsEmpty => _volumes.Count == 0;

    /// <summary>The regions erased, for reporting what a map's phantoms actually remove.</summary>
    public IReadOnlyList<BlockBox> Boxes => [.. _volumes.Select(v => v.Box)];

    /// <summary>Whether the block of <paramref name="blockId"/> at these coordinates is deleted before the
    /// match starts. False for every block of every map with no erasing phantom.</summary>
    public bool Erases(int x, int y, int z, int blockId)
    {
        foreach (var (box, ids) in _volumes)
            if (ids.Contains(blockId)
                && x >= box.MinX && x <= box.MaxX
                && y >= box.MinY && y <= box.MaxY
                && z >= box.MinZ && z <= box.MaxZ) return true;
        return false;
    }

    /// <summary>Derive what a parsed map erases before play. Empty unless the map has a block-swap phantom
    /// whose mode replaces it with air at zero seconds.</summary>
    public static PhantomErasure From(MapXml map)
    {
        var volumes = new List<(BlockBox, IReadOnlySet<int>)>();

        foreach (var destroyable in map.Destroyables)
        {
            if (destroyable.Phantom != PhantomKind.BlockSwap) continue;

            var modes = destroyable.ModeChanges
                ? map.Modes
                : map.Modes.Where(m => destroyable.Modes?.Contains(m.Id) == true);
            if (!modes.Any(m => ErasesToAir(m))) continue;

            var ids = MaterialIds.Resolve(destroyable.Materials);
            if (ids.Count == 0) continue;                  // an unreadable material erases nothing

            if (!map.Regions.TryGetValue(destroyable.RegionId, out var region)) continue;
            foreach (var box in RegionBoxes.Of(map.Regions, region)) volumes.Add((box, ids));
        }
        return volumes.Count == 0 ? None : new PhantomErasure(volumes);
    }

    // A mode erases when it replaces the blocks with air, and it does so before play only at zero seconds.
    // PGM durations are ISO-ish shorthand (`0s`, `15m`); anything that is not recognisably zero is treated as
    // mid-match, which keeps the blocks as terrain.
    private static bool ErasesToAir(ObjectiveMode mode) =>
        mode.Material.Trim().Equals("air", StringComparison.OrdinalIgnoreCase) && IsZeroDuration(mode.After);

    private static bool IsZeroDuration(string after)
    {
        var text = after.Trim().TrimStart('P', 'T').TrimEnd('s', 'S', 'm', 'M', 'h', 'H', 'd', 'D');
        return text.Length == 0 || (double.TryParse(text, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var value) && value == 0);
    }
}
