using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using PgmStudio.Data.Schema;

namespace PgmStudio.Data.Theme;

/// <summary>
/// Persistence for the prop recipes a placement names (the M0030 tables): trees and boulders.
///
/// <para><see cref="HousePartStore"/>'s sibling, and deliberately row-level for the same reason — turning a row
/// into the recipe the pass consumes needs the dressing model, so that happens a layer up.</para>
///
/// <para><b>Nothing binds these rows.</b> A room style holds a roof's id, so deleting a roof has to ask who is
/// using it; a placement names a recipe by a key in its <em>own document's</em> registry, which is a copy the
/// pull made. So a recipe is deleted without a question, and no map changes when it goes.</para>
/// </summary>
public sealed class PropStyleStore(PgmDb db)
{
    // ── trees ─────────────────────────────────────────────────────────────────────────────────────────
    public Task<List<TreeStyleRow>> ListTreesAsync(CancellationToken ct = default)
        => db.TreeStyles.OrderByDescending(r => r.Id).ToListAsync(ct);

    public Task<TreeStyleRow?> GetTreeAsync(long id, CancellationToken ct = default)
        => db.TreeStyles.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<long> CreateTreeAsync(TreeStyleRow tree, CancellationToken ct = default)
    {
        tree.CreatedAt = DateTime.UtcNow;
        return await db.InsertWithInt64IdentityAsync(tree, token: ct);
    }

    public async Task<bool> UpdateTreeAsync(long id, TreeStyleRow tree, CancellationToken ct = default)
        => await db.TreeStyles.Where(r => r.Id == id)
            .Set(r => r.Name, tree.Name)
            .Set(r => r.Form, tree.Form)
            .Set(r => r.Species, tree.Species)
            .Set(r => r.Wood, tree.Wood)
            .Set(r => r.Height, tree.Height)
            .Set(r => r.Stems, tree.Stems)
            .Set(r => r.Leader, tree.Leader)
            .Set(r => r.Flow, tree.Flow)
            .Set(r => r.BranchAngle, tree.BranchAngle)
            .Set(r => r.Levels, tree.Levels)
            .Set(r => r.Whorled, tree.Whorled)
            .Set(r => r.LeafSize, tree.LeafSize)
            .UpdateAsync(ct) > 0;

    public Task<int> DeleteTreeAsync(long id, CancellationToken ct = default)
        => db.TreeStyles.Where(r => r.Id == id).DeleteAsync(ct);

    // ── boulders ──────────────────────────────────────────────────────────────────────────────────────
    public Task<List<BoulderStyleRow>> ListBouldersAsync(CancellationToken ct = default)
        => db.BoulderStyles.OrderByDescending(r => r.Id).ToListAsync(ct);

    public Task<BoulderStyleRow?> GetBoulderAsync(long id, CancellationToken ct = default)
        => db.BoulderStyles.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<long> CreateBoulderAsync(BoulderStyleRow boulder, CancellationToken ct = default)
    {
        boulder.CreatedAt = DateTime.UtcNow;
        return await db.InsertWithInt64IdentityAsync(boulder, token: ct);
    }

    public async Task<bool> UpdateBoulderAsync(long id, BoulderStyleRow boulder, CancellationToken ct = default)
        => await db.BoulderStyles.Where(r => r.Id == id)
            .Set(r => r.Name, boulder.Name)
            .Set(r => r.Form, boulder.Form)
            .Set(r => r.Size, boulder.Size)
            .Set(r => r.Mossy, boulder.Mossy)
            .Set(r => r.Rock, boulder.Rock)
            .UpdateAsync(ct) > 0;

    public Task<int> DeleteBoulderAsync(long id, CancellationToken ct = default)
        => db.BoulderStyles.Where(r => r.Id == id).DeleteAsync(ct);
}
