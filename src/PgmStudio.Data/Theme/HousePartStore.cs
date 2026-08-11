using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using PgmStudio.Data.Schema;

namespace PgmStudio.Data.Theme;

/// <summary>
/// Persistence for the part styles a house is composed from (the M0018 tables, B71): roofs, storeys and
/// porches. <see cref="RoomStyleStore"/>'s sibling one level down — a room style binds these the way a theme
/// binds styles — and, like it, deliberately row-level: turning courses into the <c>HouseStyle</c> the stamper
/// consumes needs the material model, so that happens a layer up.
///
/// <para>Roofs and storeys carry course stacks; a porch does not, because a porch stands on the house's own
/// floor and under a canopy in the roof's own material, so what is left to it is its shape.</para>
/// </summary>
public sealed class HousePartStore(PgmDb db)
{
    // ── roofs ─────────────────────────────────────────────────────────────────────────────────────────
    public Task<List<RoofStyleRow>> ListRoofsAsync(CancellationToken ct = default)
        => db.RoofStyles.OrderByDescending(r => r.Id).ToListAsync(ct);

    public Task<RoofStyleRow?> GetRoofAsync(long id, CancellationToken ct = default)
        => db.RoofStyles.FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<List<RoofStyleCourseRow>> GetRoofCoursesAsync(long roofStyleId, CancellationToken ct = default)
        => db.RoofStyleCourses.Where(c => c.RoofStyleId == roofStyleId)
            .OrderBy(c => c.Part).ThenBy(c => c.Ordinal).ToListAsync(ct);

    /// <summary>Every roof course in the library — the whole set in one read, so listing roofs with a picture
    /// of each does not cost a query per row.</summary>
    public Task<List<RoofStyleCourseRow>> GetAllRoofCoursesAsync(CancellationToken ct = default)
        => db.RoofStyleCourses.OrderBy(c => c.Part).ThenBy(c => c.Ordinal).ToListAsync(ct);

    public async Task<long> CreateRoofAsync(
        RoofStyleRow roof, IEnumerable<RoofStyleCourseRow> courses, CancellationToken ct = default)
    {
        roof.CreatedAt = DateTime.UtcNow;
        await using var tx = await db.BeginTransactionAsync(ct);
        var id = await db.InsertWithInt64IdentityAsync(roof, token: ct);
        foreach (var course in courses) { course.RoofStyleId = id; await db.InsertAsync(course, token: ct); }
        await tx.CommitAsync(ct);
        return id;
    }

    public async Task<bool> UpdateRoofAsync(
        long id, RoofStyleRow roof, IEnumerable<RoofStyleCourseRow> courses, CancellationToken ct = default)
    {
        await using var tx = await db.BeginTransactionAsync(ct);
        var updated = await db.RoofStyles.Where(r => r.Id == id)
            .Set(r => r.Name, roof.Name)
            .Set(r => r.Form, roof.Form)
            .Set(r => r.Thickness, roof.Thickness)
            .Set(r => r.Pitch, roof.Pitch)
            .Set(r => r.Overhang, roof.Overhang)
            .Set(r => r.RoofHole, roof.RoofHole)
            .Set(r => r.RidgeCap, roof.RidgeCap)
            .UpdateAsync(ct);
        if (updated == 0) return false;

        await db.RoofStyleCourses.Where(c => c.RoofStyleId == id).DeleteAsync(ct);
        foreach (var course in courses) { course.RoofStyleId = id; await db.InsertAsync(course, token: ct); }
        await tx.CommitAsync(ct);
        return true;
    }

    public Task<int> DeleteRoofAsync(long id, CancellationToken ct = default)
        => db.RoofStyles.Where(r => r.Id == id).DeleteAsync(ct);     // roof_style_course cascades (M0018)

    /// <summary>The names of the houses still binding this roof, newest first. A part bound by a house is one
    /// the library must refuse to forget — the answer a style already gives a theme.</summary>
    public Task<List<string>> UsingRoofAsync(long roofStyleId, CancellationToken ct = default)
        => db.RoomStyles.Where(r => r.RoofStyleId == roofStyleId)
            .OrderByDescending(r => r.Id).Select(r => r.Name).Distinct().ToListAsync(ct);

    // ── storeys ───────────────────────────────────────────────────────────────────────────────────────
    public Task<List<StoreyStyleRow>> ListStoreysAsync(CancellationToken ct = default)
        => db.StoreyStyles.OrderByDescending(r => r.Id).ToListAsync(ct);

    public Task<StoreyStyleRow?> GetStoreyAsync(long id, CancellationToken ct = default)
        => db.StoreyStyles.FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<List<StoreyStyleCourseRow>> GetStoreyCoursesAsync(long storeyStyleId, CancellationToken ct = default)
        => db.StoreyStyleCourses.Where(c => c.StoreyStyleId == storeyStyleId)
            .OrderBy(c => c.Part).ThenBy(c => c.Ordinal).ToListAsync(ct);

    public Task<List<StoreyStyleCourseRow>> GetAllStoreyCoursesAsync(CancellationToken ct = default)
        => db.StoreyStyleCourses.OrderBy(c => c.Part).ThenBy(c => c.Ordinal).ToListAsync(ct);

    public async Task<long> CreateStoreyAsync(
        StoreyStyleRow storey, IEnumerable<StoreyStyleCourseRow> courses, CancellationToken ct = default)
    {
        storey.CreatedAt = DateTime.UtcNow;
        await using var tx = await db.BeginTransactionAsync(ct);
        var id = await db.InsertWithInt64IdentityAsync(storey, token: ct);
        foreach (var course in courses) { course.StoreyStyleId = id; await db.InsertAsync(course, token: ct); }
        await tx.CommitAsync(ct);
        return id;
    }

    public async Task<bool> UpdateStoreyAsync(
        long id, StoreyStyleRow storey, IEnumerable<StoreyStyleCourseRow> courses, CancellationToken ct = default)
    {
        await using var tx = await db.BeginTransactionAsync(ct);
        var updated = await db.StoreyStyles.Where(r => r.Id == id)
            .Set(r => r.Name, storey.Name)
            .Set(r => r.Clear, storey.Clear)
            .Set(r => r.BorderWidth, storey.BorderWidth)
            .Set(r => r.InlayInset, storey.InlayInset)
            .Set(r => r.WindowForm, storey.WindowForm)
            .Set(r => r.WindowBlock, storey.WindowBlock)
            .Set(r => r.WindowData, storey.WindowData)
            .Set(r => r.WindowSill, storey.WindowSill)
            .Set(r => r.WindowWidth, storey.WindowWidth)
            .Set(r => r.WindowHeight, storey.WindowHeight)
            .Set(r => r.WindowSpacing, storey.WindowSpacing)
            .UpdateAsync(ct);
        if (updated == 0) return false;

        await db.StoreyStyleCourses.Where(c => c.StoreyStyleId == id).DeleteAsync(ct);
        foreach (var course in courses) { course.StoreyStyleId = id; await db.InsertAsync(course, token: ct); }
        await tx.CommitAsync(ct);
        return true;
    }

    public Task<int> DeleteStoreyAsync(long id, CancellationToken ct = default)
        => db.StoreyStyles.Where(r => r.Id == id).DeleteAsync(ct);

    public Task<List<string>> UsingStoreyAsync(long storeyStyleId, CancellationToken ct = default)
        => (from s in db.RoomStyleStoreys
            join r in db.RoomStyles on s.RoomStyleId equals r.Id
            where s.StoreyStyleId == storeyStyleId
            orderby r.Id descending
            select r.Name).Distinct().ToListAsync(ct);

    // ── porches ───────────────────────────────────────────────────────────────────────────────────────
    public Task<List<PorchStyleRow>> ListPorchesAsync(CancellationToken ct = default)
        => db.PorchStyles.OrderByDescending(r => r.Id).ToListAsync(ct);

    public Task<PorchStyleRow?> GetPorchAsync(long id, CancellationToken ct = default)
        => db.PorchStyles.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<long> CreatePorchAsync(PorchStyleRow porch, CancellationToken ct = default)
    {
        porch.CreatedAt = DateTime.UtcNow;
        return await db.InsertWithInt64IdentityAsync(porch, token: ct);
    }

    public async Task<bool> UpdatePorchAsync(long id, PorchStyleRow porch, CancellationToken ct = default)
        => await db.PorchStyles.Where(r => r.Id == id)
            .Set(r => r.Name, porch.Name)
            .Set(r => r.Depth, porch.Depth)
            .Set(r => r.Inset, porch.Inset)
            .Set(r => r.Edge, porch.Edge)
            .Set(r => r.RoofForm, porch.RoofForm)
            .Set(r => r.RailBlock, porch.RailBlock)
            .UpdateAsync(ct) > 0;

    public Task<int> DeletePorchAsync(long id, CancellationToken ct = default)
        => db.PorchStyles.Where(r => r.Id == id).DeleteAsync(ct);

    public Task<List<string>> UsingPorchAsync(long porchStyleId, CancellationToken ct = default)
        => db.RoomStyles.Where(r => r.PorchStyleId == porchStyleId)
            .OrderByDescending(r => r.Id).Select(r => r.Name).Distinct().ToListAsync(ct);
}
