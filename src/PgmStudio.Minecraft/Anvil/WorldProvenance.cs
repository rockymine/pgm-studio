using PgmStudio.Domain;

namespace PgmStudio.Minecraft.Anvil;

/// <summary>
/// Which build pass claimed a column, last. <see cref="Ground"/> is the rasterizer's own terrain — whatever
/// the painter later finishes it with, a plaza included. <see cref="Structure"/> is anything raised over it
/// as a built thing: a wool cage, a spawn cube, a wall, an objective, a dressing-placed building.
/// <see cref="Prop"/> is everything else the dressing pass put down — a tree, a boulder, flora, a path, a
/// water course — which is placed rather than built and must not read as a building.
///
/// <para><b><see cref="Prop"/> exists because a material test answers a different question.</b> A tree does
/// separate from built ground by material, which is the argument for recording only the first two. What
/// material cannot say is that a <em>pass</em> put it there, or which prop it belonged to.
/// Those are the two things a read-back has to prove — a flora prop that landed nothing looks exactly like a
/// flora prop that was never authored, and two trees of one orbit cannot be told from two trees that happen
/// to stand alike.</para>
/// </summary>
public enum ProvenancePass { Ground, Structure, Prop }

/// <summary>
/// Which pass claimed each column of a world the studio built, and — for a <see cref="ProvenancePass.Structure"/>
/// claim — which stamped thing did the claiming, recorded beside the voxels because a block carries no
/// provenance byte of its own (<c>docs/world-export/decoration.md</c>).
///
/// <para><b>Composited in placement order.</b> The rasterizer claims every column it lays as <see
/// cref="ProvenancePass.Ground"/> first; every stamp that follows — a room floor, a wool cage, a spawn cube,
/// a wall, an iron cube, a redstone line, a destroyable, a core, a dressing-placed building — claims its own
/// footprint as <see cref="ProvenancePass.Structure"/> over it. A later claim always overwrites an earlier
/// one, so the final answer at a column is whichever pass claimed it last, exactly the reading
/// <c>docs/tools/mapgen-review.md</c> asks for: a stone-brick plaza the painter finishes
/// stays <see cref="ProvenancePass.Ground"/> because nothing after the rasterizer claims it, and a
/// stone-brick cottage on that same plaza is <see cref="ProvenancePass.Structure"/> because the house
/// stamp claims its footprint afterwards — two different things because two different passes put them
/// there, whatever they are made of.</para>
///
/// <para><b>The owner is the claim's identity, not its material.</b> Two houses that genuinely touch — a
/// terrace, a shared wall — are two different claims even where their columns are neighbours, because the
/// pass that stamps each one already knows which thing it is stamping: a dressing prop carries its own
/// <c>Id</c>, a wool cage belongs to a room, a spawn to a team, a destroyable to its marker. Recording that
/// alongside the pass is what lets a reader (<see cref="Render.StructureFinder"/>) tell two abutting
/// buildings apart by grouping on the owner rather than by flooding across every contiguous claimed column,
/// which cannot distinguish them at all. A null owner is every <see cref="ProvenancePass.Ground"/> claim —
/// the terrain is not a "thing" a render would ever need to tell apart from the terrain beside it — and is
/// also what an unidentified claim degrades to: cells sharing it are read as one claim, so a pass with
/// something to distinguish always gives it a real <see cref="StampId"/>.</para>
///
/// <para><b>The id says which image of which unit, not merely which entry.</b> A stamp and its own mirror
/// carry the same <see cref="StampId.Identity"/> and differ only in <see cref="StampId.Image"/>, so a reader
/// pairs the two halves of a board by asking rather than by matching cell sets geometrically and hoping.</para>
///
/// <para>A world the studio only <em>scanned</em> carries no provenance at all: nothing recorded what placed
/// its blocks, so a renderer falls back to the material estimate (<see cref="RenderCategories.Of(int)"/>)
/// and says so rather than pretending to a certainty it does not have.</para>
/// </summary>
public sealed class WorldProvenance
{
    private readonly Dictionary<(int X, int Z), (ProvenancePass Pass, StampId? Owner)> _claims = [];

    /// <summary>Claim one column. A later call for the same column overwrites an earlier one — the whole of
    /// how "placement order" is expressed. <paramref name="owner"/> null is a claim nothing identified: the
    /// terrain, which is not a "thing" a reader tells apart from the terrain beside it, and the degraded
    /// reading for a stamp that had no id to give.</summary>
    public void Claim(int x, int z, ProvenancePass pass, StampId? owner = null) => _claims[(x, z)] = (pass, owner);

    /// <summary>Claim every column in a set, cheaper to call at a stamp site than looping by hand.</summary>
    public void Claim(IEnumerable<(int X, int Z)> cells, ProvenancePass pass, StampId? owner = null)
    {
        foreach (var cell in cells) _claims[cell] = (pass, owner);
    }

    /// <summary>Claim every column in an inclusive rectangle — the shape almost every stamp's footprint
    /// already is.</summary>
    public void ClaimRect(int minX, int minZ, int maxX, int maxZ, ProvenancePass pass, StampId? owner = null)
    {
        for (var z = minZ; z <= maxZ; z++)
        for (var x = minX; x <= maxX; x++)
            _claims[(x, z)] = (pass, owner);
    }

    /// <summary>The pass that claimed a column last, or null when nothing ever claimed it — a hole in the
    /// rasterized ground, or (for a scanned world) provenance that was never recorded at all.</summary>
    public ProvenancePass? PassAt(int x, int z) => _claims.TryGetValue((x, z), out var claim) ? claim.Pass : null;

    /// <summary>The stamp that claimed a column last, or null both when nothing ever claimed it and when what
    /// claimed it had no id — the terrain always, and a stamp that identified nothing. Ask
    /// <see cref="PassAt"/> to tell the two apart.</summary>
    public StampId? OwnerAt(int x, int z) => _claims.TryGetValue((x, z), out var claim) ? claim.Owner : null;

    public int Count => _claims.Count;

    /// <summary>Every claimed column, its pass and its owner, in no particular order — what a caller
    /// persisting the record (<see cref="WorldProvenanceFile"/>) or testing it walks.</summary>
    public IEnumerable<((int X, int Z) Cell, ProvenancePass Pass, StampId? Owner)> Claims =>
        _claims.Select(entry => (entry.Key, entry.Value.Pass, entry.Value.Owner));
}
