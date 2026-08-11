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
        RoomStyleRow room, IEnumerable<RoomStyleCourseRow> courses, CancellationToken ct = default)
    {
        room.CreatedAt = DateTime.UtcNow;
        await using var tx = await db.BeginTransactionAsync(ct);
        var id = await db.InsertWithInt64IdentityAsync(room, token: ct);
        foreach (var course in courses)
        {
            course.RoomStyleId = id;
            await db.InsertAsync(course, token: ct);
        }
        await tx.CommitAsync(ct);
        return id;
    }

    /// <summary>Replace a room style's knobs and its whole set of courses in one transaction, returning false
    /// when the id is unknown. The courses are rewritten rather than merged, for the reason a theme's bindings
    /// are: the stack itself is what the caller edited, so a diff would only be a slower way to the same rows.</summary>
    public async Task<bool> UpdateAsync(
        long id, RoomStyleRow room, IEnumerable<RoomStyleCourseRow> courses, CancellationToken ct = default)
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
            .Set(r => r.Door, room.Door)
            .Set(r => r.DoorHeight, room.DoorHeight)
            .UpdateAsync(ct);
        if (updated == 0) return false;

        await db.RoomStyleCourses.Where(c => c.RoomStyleId == id).DeleteAsync(ct);
        foreach (var course in courses)
        {
            course.RoomStyleId = id;
            await db.InsertAsync(course, token: ct);
        }
        await tx.CommitAsync(ct);
        return true;
    }

    public Task<int> DeleteAsync(long id, CancellationToken ct = default)
        => db.RoomStyles.Where(r => r.Id == id).DeleteAsync(ct);   // room_style_course cascades (M0012)
}
