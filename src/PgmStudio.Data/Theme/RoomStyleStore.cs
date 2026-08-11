using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using PgmStudio.Data.Schema;

namespace PgmStudio.Data.Theme;

/// <summary>
/// Persistence for the room-style library (the M0012 tables, G34b). <see cref="ThemeStore"/>'s sibling and
/// deliberately its shape: a <see cref="RoomStyleRow"/> plus its <see cref="RoomStyleCourseRow"/> bindings is
/// a composition of the same <see cref="StyleRow"/> library a theme composes from. It stays row-level —
/// turning courses into the <c>HouseStyle</c> the stamper consumes needs the material model, so it happens a
/// layer up.
/// </summary>
public sealed class RoomStyleStore(PgmDb db)
{
    public Task<List<RoomStyleRow>> ListAsync(CancellationToken ct = default)
        => db.RoomStyles.OrderByDescending(r => r.Id).ToListAsync(ct);

    public Task<RoomStyleRow?> GetAsync(long id, CancellationToken ct = default)
        => db.RoomStyles.FirstOrDefaultAsync(r => r.Id == id, ct);

    /// <summary>One house's storeys, ground first — which storey style fills each and the clear it takes
    /// there. Owned by the house rather than by the part library: the stack is what a house <em>is</em>, and
    /// a storey style knows nothing about the buildings that use it.</summary>
    public Task<List<RoomStyleStoreyRow>> GetStoreysAsync(long roomStyleId, CancellationToken ct = default)
        => db.RoomStyleStoreys.Where(s => s.RoomStyleId == roomStyleId).OrderBy(s => s.Ordinal).ToListAsync(ct);

    /// <summary>Every house's stack in one read, for listing the library.</summary>
    public Task<List<RoomStyleStoreyRow>> GetAllStoreysAsync(CancellationToken ct = default)
        => db.RoomStyleStoreys.OrderBy(s => s.RoomStyleId).ThenBy(s => s.Ordinal).ToListAsync(ct);

    /// <summary>One room style's courses, in stack order per part.</summary>
    public Task<List<RoomStyleCourseRow>> GetCoursesAsync(long roomStyleId, CancellationToken ct = default)
        => db.RoomStyleCourses.Where(c => c.RoomStyleId == roomStyleId)
            .OrderBy(c => c.Part).ThenBy(c => c.Ordinal).ToListAsync(ct);

    /// <summary>Every course of every room style, joined to the style it binds — the whole library in one read,
    /// so listing rooms with a picture of each does not cost a query per row.</summary>
    public async Task<List<(RoomStyleCourseRow Course, StyleRow Style)>> GetAllCourseStylesAsync(
        CancellationToken ct = default)
        => (await (from c in db.RoomStyleCourses
                   join s in db.Styles on c.StyleId equals s.Id
                   orderby c.Part, c.Ordinal
                   select new { c, s }).ToListAsync(ct))
            .Select(row => (row.c, row.s)).ToList();

    /// <summary>The names of the room styles still binding a style, newest first. A style is shared with the
    /// theme library, so a caller asks both before deleting one.</summary>
    public Task<List<string>> UsingStyleAsync(long styleId, CancellationToken ct = default)
        => (from c in db.RoomStyleCourses
            join r in db.RoomStyles on c.RoomStyleId equals r.Id
            where c.StyleId == styleId
            orderby r.Id descending
            select r.Name).Distinct().ToListAsync(ct);

    public async Task<long> CreateAsync(
        RoomStyleRow room, IEnumerable<RoomStyleCourseRow> courses,
        IEnumerable<RoomStyleStoreyRow>? storeys = null, CancellationToken ct = default)
    {
        room.CreatedAt = DateTime.UtcNow;
        await using var tx = await db.BeginTransactionAsync(ct);
        var id = await db.InsertWithInt64IdentityAsync(room, token: ct);
        foreach (var course in courses)
        {
            course.RoomStyleId = id;
            await db.InsertAsync(course, token: ct);
        }
        await WriteStoreysAsync(id, storeys, ct);
        await tx.CommitAsync(ct);
        return id;
    }

    /// <summary>Replace a room style's knobs and its whole set of courses in one transaction, returning false
    /// when the id is unknown. The courses are rewritten rather than merged, for the reason a theme's bindings
    /// are: the stack itself is what the caller edited, so a diff would only be a slower way to the same rows.</summary>
    public async Task<bool> UpdateAsync(
        long id, RoomStyleRow room, IEnumerable<RoomStyleCourseRow> courses,
        IEnumerable<RoomStyleStoreyRow>? storeys = null, CancellationToken ct = default)
    {
        await using var tx = await db.BeginTransactionAsync(ct);
        var updated = await db.RoomStyles.Where(r => r.Id == id)
            .Set(r => r.Name, room.Name)
            .Set(r => r.FloorDepth, room.FloorDepth)
            .Set(r => r.WallHeight, room.WallHeight)
            .Set(r => r.RoofThickness, room.RoofThickness)
            .Set(r => r.RoofForm, room.RoofForm)
            .Set(r => r.Pitch, room.Pitch)
            .Set(r => r.Overhang, room.Overhang)
            .Set(r => r.RoofHole, room.RoofHole)
            .Set(r => r.RidgeCap, room.RidgeCap)
            .Set(r => r.Storeys, room.Storeys)
            .Set(r => r.StoreyClear, room.StoreyClear)
            .Set(r => r.Door, room.Door)
            .Set(r => r.DoorHeight, room.DoorHeight)
            .Set(r => r.BorderWidth, room.BorderWidth)
            .Set(r => r.InlayInset, room.InlayInset)
            .Set(r => r.WindowForm, room.WindowForm)
            .Set(r => r.WindowBlock, room.WindowBlock)
            .Set(r => r.WindowData, room.WindowData)
            .Set(r => r.WindowSill, room.WindowSill)
            .Set(r => r.WindowWidth, room.WindowWidth)
            .Set(r => r.WindowHeight, room.WindowHeight)
            .Set(r => r.WindowSpacing, room.WindowSpacing)
            .Set(r => r.PorchDepth, room.PorchDepth)
            .Set(r => r.PorchInset, room.PorchInset)
            .Set(r => r.PorchEdge, room.PorchEdge)
            .Set(r => r.PorchRoof, room.PorchRoof)
            .Set(r => r.PorchRailBlock, room.PorchRailBlock)
            // The M0019 trim. A column added to the row and not to this list is a column the editor can save
            // and the store silently drops — which is exactly how the seeder's first run came back stripped.
            .Set(r => r.BeamBlock, room.BeamBlock)
            .Set(r => r.BeamData, room.BeamData)
            .Set(r => r.BeamReach, room.BeamReach)
            .Set(r => r.RoofSlab, room.RoofSlab)
            .Set(r => r.RoofSlabData, room.RoofSlabData)
            .Set(r => r.GableWindowForm, room.GableWindowForm)
            .Set(r => r.GableWindowBlock, room.GableWindowBlock)
            .Set(r => r.GableWindowData, room.GableWindowData)
            .Set(r => r.GableWindowSill, room.GableWindowSill)
            .Set(r => r.GableWindowWidth, room.GableWindowWidth)
            .Set(r => r.GableWindowHeight, room.GableWindowHeight)
            .Set(r => r.DoorHeadForm, room.DoorHeadForm)
            .Set(r => r.DoorHeadBlock, room.DoorHeadBlock)
            .Set(r => r.DoorHeadFill, room.DoorHeadFill)
            .Set(r => r.DoorHeadFillBlock, room.DoorHeadFillBlock)
            .Set(r => r.DoorHeadFillData, room.DoorHeadFillData)
            .Set(r => r.WindowHostBlock, room.WindowHostBlock)
            .Set(r => r.WindowHostData, room.WindowHostData)
            .Set(r => r.RoofStyleId, room.RoofStyleId)
            .Set(r => r.PorchStyleId, room.PorchStyleId)
            .UpdateAsync(ct);
        if (updated == 0) return false;

        await db.RoomStyleCourses.Where(c => c.RoomStyleId == id).DeleteAsync(ct);
        foreach (var course in courses)
        {
            course.RoomStyleId = id;
            await db.InsertAsync(course, token: ct);
        }
        await WriteStoreysAsync(id, storeys, ct);
        await tx.CommitAsync(ct);
        return true;
    }

    /// <summary>The house's stack, rewritten. The ordinal is assigned here rather than trusted from the
    /// caller: the stack is an <em>order</em>, and the order is the position in the list it arrived in — a
    /// caller free to number them could save a stack with two ground floors and no first.</summary>
    private async Task WriteStoreysAsync(
        long roomStyleId, IEnumerable<RoomStyleStoreyRow>? storeys, CancellationToken ct)
    {
        if (storeys is null) return;
        await db.RoomStyleStoreys.Where(s => s.RoomStyleId == roomStyleId).DeleteAsync(ct);
        var ordinal = 0;
        foreach (var storey in storeys)
        {
            storey.RoomStyleId = roomStyleId;
            storey.Ordinal = ordinal++;
            await db.InsertAsync(storey, token: ct);
        }
    }

    public Task<int> DeleteAsync(long id, CancellationToken ct = default)
        => db.RoomStyles.Where(r => r.Id == id).DeleteAsync(ct);   // its courses and stack cascade (M0012, M0018)
}
