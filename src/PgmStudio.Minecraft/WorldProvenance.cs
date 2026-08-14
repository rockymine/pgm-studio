namespace PgmStudio.Minecraft;

/// <summary>Which build pass claimed a column, last. <see cref="Ground"/> is the rasterizer's own terrain —
/// whatever the painter later finishes it with, a plaza included — and <see cref="Structure"/> is anything a
/// stamper or the dressing pass raised over it: a wool cage, a spawn cube, a wall, an objective, a
/// dressing-placed building. There is no third value for a tree, a boulder or flora, because those already
/// separate from a built surface by material without ambiguity (<see cref="BlockRoles"/>) — provenance exists
/// for exactly the pair a material test cannot tell apart.</summary>
public enum ProvenanceLayer { Ground, Structure }

/// <summary>
/// Which pass claimed each column of a world the studio built, recorded beside the voxels because a block
/// carries no provenance byte of its own (<c>docs/world-export/decoration.md</c>).
///
/// <para><b>Composited in placement order.</b> The rasterizer claims every column it lays as <see
/// cref="ProvenanceLayer.Ground"/> first; every stamp that follows — a room floor, a wool cage, a spawn cube,
/// a wall, an iron cube, a redstone line, a destroyable, a core, a dressing-placed building — claims its own
/// footprint as <see cref="ProvenanceLayer.Structure"/> over it. A later claim always overwrites an earlier
/// one, so the final answer at a column is whichever pass claimed it last, exactly the reading
/// <c>docs/tools/mapgen-review.md</c>'s B133 finding asked for: a stone-brick plaza the painter finishes
/// stays <see cref="ProvenanceLayer.Ground"/> because nothing after the rasterizer claims it, and a
/// stone-brick cottage on that same plaza is <see cref="ProvenanceLayer.Structure"/> because the house
/// stamp claims its footprint afterwards — two different things because two different passes put them
/// there, whatever they are made of.</para>
///
/// <para>A world the studio only <em>scanned</em> carries no provenance at all: nothing recorded what placed
/// its blocks, so a renderer falls back to the material estimate (<see cref="RenderCategories.Of(int)"/>)
/// and says so rather than pretending to a certainty it does not have.</para>
/// </summary>
public sealed class WorldProvenance
{
    private readonly Dictionary<(int X, int Z), ProvenanceLayer> _claims = [];

    /// <summary>Claim one column. A later call for the same column overwrites an earlier one — the whole of
    /// how "placement order" is expressed.</summary>
    public void Claim(int x, int z, ProvenanceLayer layer) => _claims[(x, z)] = layer;

    /// <summary>Claim every column in a set, cheaper to call at a stamp site than looping by hand.</summary>
    public void Claim(IEnumerable<(int X, int Z)> cells, ProvenanceLayer layer)
    {
        foreach (var cell in cells) _claims[cell] = layer;
    }

    /// <summary>Claim every column in an inclusive rectangle — the shape almost every stamp's footprint
    /// already is.</summary>
    public void ClaimRect(int minX, int minZ, int maxX, int maxZ, ProvenanceLayer layer)
    {
        for (var z = minZ; z <= maxZ; z++)
        for (var x = minX; x <= maxX; x++)
            _claims[(x, z)] = layer;
    }

    /// <summary>The layer that claimed a column last, or null when nothing ever claimed it — a hole in the
    /// rasterized ground, or (for a scanned world) provenance that was never recorded at all.</summary>
    public ProvenanceLayer? LayerAt(int x, int z) => _claims.TryGetValue((x, z), out var layer) ? layer : null;

    public int Count => _claims.Count;

    /// <summary>Every claimed column and its layer, in no particular order — what a caller persisting the
    /// record (<see cref="WorldProvenanceFile"/>) or testing it walks.</summary>
    public IEnumerable<((int X, int Z) Cell, ProvenanceLayer Layer)> Claims =>
        _claims.Select(entry => (entry.Key, entry.Value));
}
