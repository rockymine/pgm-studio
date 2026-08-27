using PgmStudio.Domain;
using PgmStudio.Contracts;
using PgmStudio.Data.Schema;
using PgmStudio.Data.Theme;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Houses;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Services;

/// <summary>
/// Puts the built-in presets into a library: the materials the showcase paints its plateaus with, and the
/// <see cref="HousePresets"/> the house row is built from.
///
/// <para><b>Idempotent, keyed by name.</b> A library is something an author edits, so a seeder that dropped and
/// rewrote its rows would throw away work every time it ran. A row whose name is already there is updated in
/// place and keeps its id — which is what the maps and themes binding it depend on — and a name that is not
/// there is created. Nothing is ever deleted, so a name retired from the presets simply stays in the library as
/// a row the author now owns.</para>
///
/// <para>It is the inverse of <see cref="RoomStyleLibrary.Compose"/> and lives beside it for that reason: one
/// of them turns rows into a building and the other a building into rows, and a change to what a room style
/// carries has to land in both or the seed stops round-tripping. <see cref="VerifyAsync"/> is what makes that
/// checkable — it composes each seeded row back and reports what the store could not hold.</para>
/// </summary>
public sealed class LibrarySeed(ThemeStore styles, RoomStyleStore rooms, HousePartStore parts)
{
    /// <summary>What one run did, so a caller can say so rather than guessing from silence.</summary>
    public readonly record struct Tally(
        int StylesAdded, int StylesUpdated, int RoomsAdded, int RoomsUpdated, int ThemesAdded, int ThemesUpdated);

    /// <summary>Seed everything: the materials first, since a room style's courses bind them by id.</summary>
    public async Task<Tally> SeedAsync(CancellationToken ct = default)
    {
        var bound = await SeedStylesAsync(ct);
        var (added, updated) = await SeedHousesAsync(bound, ct);
        var built = await SeedPartsAsync(bound, ct);
        var themes = await SeedThemesAsync(ct);
        return new Tally(
            bound.Added, bound.Updated, added + built.Added, updated + built.Updated,
            themes.Added, themes.Updated);
    }

    // ── the roofs and the porches ─────────────────────────────────────────────────────────────────────
    /// <summary>Each preset's roof and porch as rows of their own, keyed by name. Without them a studio opens
    /// two of its six libraries on nothing, though every preset in the house library is wearing one.</summary>
    private async Task<(int Added, int Updated)> SeedPartsAsync(StyleIds bound, CancellationToken ct)
    {
        var roofs = (await parts.ListRoofsAsync(ct))
            .GroupBy(row => row.Name)
            .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.OrdinalIgnoreCase);
        var porches = (await parts.ListPorchesAsync(ct))
            .GroupBy(row => row.Name)
            .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.OrdinalIgnoreCase);

        int added = 0, updated = 0;
        foreach (var house in HousePresets.All)
        {
            var roofName = $"{house.Name} · roof";
            var roofRequest = RoofRequestFor(house, roofName, bound.ByName);
            var roofRow = HousePartLibrary.RowOf(roofRequest);
            var roofCourses = HousePartLibrary.RoofCourseRowsOf(roofRequest);
            if (roofs.TryGetValue(roofName, out var roofId))
            {
                await parts.UpdateRoofAsync(roofId, roofRow, roofCourses, ct);
                updated++;
            }
            else
            {
                await parts.CreateRoofAsync(roofRow, roofCourses, ct);
                added++;
            }

            if (house.Style.Porch is not { } porch) continue;
            var porchName = $"{house.Name} · porch";
            var porchRow = HousePartLibrary.RowOf(new PorchStyleSaveRequest(
                porchName, porch.Depth, porch.Inset, PorchEdges.Canonical(NameOf(porch.Edge)),
                RoofForms.Canonical(NameOf(porch.Roof)), porch.RailBlock));
            if (porches.TryGetValue(porchName, out var porchId))
            {
                await parts.UpdatePorchAsync(porchId, porchRow, ct);
                updated++;
            }
            else
            {
                await parts.CreatePorchAsync(porchRow, ct);
                added++;
            }
        }
        return (added, updated);
    }

    /// <summary>One roof as the request the library stores, binding the materials seeded under its own names.</summary>
    private static RoofStyleSaveRequest RoofRequestFor(
        HousePresets.House house, string name, IReadOnlyDictionary<string, long> ids)
    {
        var roof = house.Style.Roof;
        var courses = new List<RoomCourseDto>();
        foreach (var part in new[] { RoomParts.Roof, RoomParts.Verge, RoomParts.Gable })
            if (ids.TryGetValue($"{house.Name} · {part}", out var id))
                courses.Add(new RoomCourseDto(part, 0, id, 1));

        return new RoofStyleSaveRequest(
            Name: name, Form: RoofForms.Canonical(NameOf(roof.Form)), Pitch: roof.Pitch, Overhang: roof.Overhang,
            RoofHole: roof.Hole, RidgeCap: roof.RidgeCap, Courses: courses,
            RoofSlab: roof.Slab, RoofSlabData: roof.SlabData);
    }

    // ── the finishes ──────────────────────────────────────────────────────────────────────────────────
    /// <summary>Each preset finish as a theme row binding one style per bucket, both keyed by name. A theme
    /// composed of nothing is a tab that opens on a sentence telling an author to start one, which is the
    /// hardest thing in the library to start from nothing.</summary>
    private async Task<(int Added, int Updated)> SeedThemesAsync(CancellationToken ct)
    {
        var existingStyles = (await styles.ListStylesAsync(ct: ct))
            .GroupBy(style => style.Name)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var existingThemes = (await styles.ListThemesAsync(ct))
            .GroupBy(theme => theme.Name)
            .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.OrdinalIgnoreCase);

        int added = 0, updated = 0;
        foreach (var (name, theme) in ThemePresets.All)
        {
            var decomposed = TerrainThemeComposer.Decompose(theme);
            var buckets = new List<ThemeBucketRow>();
            foreach (var binding in decomposed.Buckets)
            {
                var bucket = ThemeLibrary.FromBucket(binding.Bucket);
                long? styleId = null;
                if (binding is { Kind: { } kind, MaterialJson: { } material })
                {
                    var styleName = $"{name} · {bucket}";
                    styleId = existingStyles.TryGetValue(styleName, out var stored)
                        ? await Rewritten(stored, styleName, kind, material, ct)
                        : await styles.CreateStyleAsync(
                            new StyleRow { Name = styleName, Kind = kind, Params = material }, ct);
                }
                buckets.Add(new ThemeBucketRow
                {
                    Bucket = bucket, StyleId = styleId, Depth = binding.Depth, Enabled = binding.Enabled,
                });
            }

            var themeRow = new ThemeRow
            {
                Name = name,
                BedrockRelative = decomposed.BedrockRelative, BedrockValue = decomposed.BedrockValue,
                RimEdges = ThemeLibrary.FromRimEdges(decomposed.RimEdges),
                WallOnTerrainFaces = decomposed.WallOnTerrainFaces,
            };
            if (existingThemes.TryGetValue(name, out var themeId))
            {
                await styles.UpdateThemeAsync(themeId, themeRow, buckets, ct);
                updated++;
            }
            else
            {
                await styles.CreateThemeAsync(themeRow, buckets, ct);
                added++;
            }
        }
        return (added, updated);
    }

    /// <summary>A bucket's style, rewritten in place where the preset has moved on from what is stored.</summary>
    private async Task<long> Rewritten(StyleRow row, string name, string kind, string material, CancellationToken ct)
    {
        if (row.Kind != kind || row.Params != material) await styles.UpdateStyleAsync(row.Id, name, kind, material, ct);
        return row.Id;
    }

    // ── the materials ─────────────────────────────────────────────────────────────────────────────────
    /// <summary>Every distinct material the presets name, stored under a stable name and mapped back to the id
    /// it landed on. The name is the key, so running twice binds the same rows rather than a second copy.</summary>
    private async Task<StyleIds> SeedStylesAsync(CancellationToken ct)
    {
        var existing = (await styles.ListStylesAsync(ct: ct))
            .GroupBy(style => style.Name)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var ids = new StyleIds();
        foreach (var (name, material) in PresetMaterials())
        {
            var kind = TerrainThemeComposer.KindOf(material);
            var json = TerrainThemeJson.Serialize(material);

            if (existing.TryGetValue(name, out var row))
            {
                // Rewritten in place rather than left alone: the preset is the source of truth for a preset's
                // own materials, and a stale one would compose a building that is not the one it names.
                if (row.Kind != kind || row.Params != json)
                {
                    await styles.UpdateStyleAsync(row.Id, name, kind, json, ct);
                    ids.Updated++;
                }
                ids.ByName[name] = row.Id;
                continue;
            }

            ids.ByName[name] = await styles.CreateStyleAsync(
                new StyleRow { Name = name, Kind = kind, Params = json }, ct);
            ids.Added++;
        }
        return ids;
    }

    /// <summary>The materials the presets use, each under the name it is stored as — the house it belongs to
    /// and the part it plays, so a library reads as a set of houses rather than as forty loose blocks.</summary>
    private static IEnumerable<(string Name, TerrainMaterial Material)> PresetMaterials()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var house in HousePresets.All)
            foreach (var (part, material) in HouseMaterials(house.Style))
            {
                var name = $"{house.Name} · {part}";
                if (seen.Add(name)) yield return (name, material);
            }
    }

    /// <summary>Every material one house names, tagged with the part it plays and the position in that part's
    /// stack. A stack's courses are numbered so two courses of one part keep distinct names.</summary>
    private static IEnumerable<(string Part, TerrainMaterial Material)> HouseMaterials(HouseStyle style)
    {
        foreach (var entry in PartCoursesOf(RoomParts.Floor, style.Foundation.Plate)) yield return entry;
        foreach (var entry in PartCoursesOf(RoomParts.Wall, style.Wall)) yield return entry;
        yield return (RoomParts.Roof, style.Roof.Body);
        yield return (RoomParts.Verge, style.Roof.Verge);
        yield return (RoomParts.Sill, style.Foundation.Footing);
        if (style.Post is { } post) yield return (RoomParts.Post, post);
        if (style.Roof.Gable is { } gable) yield return (RoomParts.Gable, gable);
        if (style.Foundation.Surface.Field is { } field) yield return (RoomParts.Field, field);
        if (style.Foundation.Surface.Border is { } border) yield return (RoomParts.Border, border);
        if (style.Foundation.Surface.Inlay is { } inlay) yield return (RoomParts.Inlay, inlay);

        // A storey's own wall and posts are materials of the building too — the library stores them on a
        // storey style, but they are the same rows and are named per storey so two storeys keep theirs apart.
        // The <em>resolved</em> storey, not the declared one: a storey that names no wall or windows of its own
        // takes the building's, and what a storey style stores is what that storey actually is. Seeding the
        // declared value instead stores an explicit "none" where the preset meant "the building's".
        for (var level = 0; level < style.Storeys.Count; level++)
        {
            var storey = style.Levels[level];
            if (storey.Wall is { } wall)
                foreach (var (part, material) in PartCoursesOf(RoomParts.Wall, wall))
                    yield return ($"storey {level + 1} {part}", material);
            if (storey.Post is { } storeyPost) yield return ($"storey {level + 1} {RoomParts.Post}", storeyPost);
            if (storey.Surface?.Field is { } storeyField) yield return ($"storey {level + 1} {RoomParts.Field}", storeyField);
        }
    }

    private static IEnumerable<(string Part, TerrainMaterial Material)> PartCoursesOf(string part, RoomPart stack)
    {
        if (stack.Stack.Bands.Count <= 1) { yield return (part, stack.At(0).Material); yield break; }
        for (var at = 0; at < stack.Stack.Bands.Count; at++)
            yield return ($"{part} {at + 1}", stack.Stack.Bands[at].Material);
    }

    private sealed class StyleIds
    {
        public Dictionary<string, long> ByName { get; } = new(StringComparer.OrdinalIgnoreCase);
        public int Added, Updated;
    }

    // ── the storeys ───────────────────────────────────────────────────────────────────────────────────
    /// <summary>Each storey of each preset as its own <c>storey_style</c> row, keyed by name and mapped back to
    /// the id the house binds it at.
    ///
    /// <para>Without these a house's stack is a <em>count</em> and one clear, so a building whose storeys differ
    /// from each other comes back as the same storey repeated — the right number of rooms and the wrong rooms.
    /// A storey style is where a storey's own wall, posts, windows and floor zoning live, which is the whole
    /// reason the level exists.</para></summary>
    private async Task<Dictionary<string, long>> SeedStoreysAsync(StyleIds bound, CancellationToken ct)
    {
        var existing = (await parts.ListStoreysAsync(ct))
            .GroupBy(row => row.Name)
            .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.OrdinalIgnoreCase);

        var ids = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var house in HousePresets.All)
            for (var level = 0; level < house.Style.Storeys.Count; level++)
            {
                var name = StoreyName(house, level);
                var request = StoreyRequestFor(house, level, name, bound.ByName);
                var row = HousePartLibrary.RowOf(request);
                var courses = HousePartLibrary.StoreyCourseRowsOf(request);

                ids[name] = existing.TryGetValue(name, out var id)
                    ? await parts.UpdateStoreyAsync(id, row, courses, ct) ? id : id
                    : await parts.CreateStoreyAsync(row, courses, ct);
            }
        return ids;
    }

    private static string StoreyName(HousePresets.House house, int level)
        => $"{house.Name} · storey {level + 1}";

    /// <summary>One storey as the request the library stores. Its materials were seeded under the same
    /// per-storey names, so binding them is a lookup rather than a second decomposition.</summary>
    private static StoreyStyleSaveRequest StoreyRequestFor(
        HousePresets.House house, int level, string name, IReadOnlyDictionary<string, long> ids)
    {
        var storey = house.Style.Levels[level];
        var courses = new List<RoomCourseDto>();

        void Bind(string part, TerrainMaterial? material, int ordinal = 0, int height = 1)
        {
            if (material is null) return;
            if (ids.TryGetValue($"{house.Name} · storey {level + 1} {part}", out var id))
                courses.Add(new RoomCourseDto(PartOf(part), ordinal, id, height));
        }

        if (storey.Wall is { } wall)
            for (var at = 0; at < wall.Stack.Bands.Count; at++)
            {
                // Named exactly as the material was seeded — one course keeps the bare part name, a stack
                // numbers its courses — and carrying each course's own height rather than the part's extent,
                // which is a different number and belongs to the part.
                var part = wall.Stack.Bands.Count <= 1 ? RoomParts.Wall : $"{RoomParts.Wall} {at + 1}";
                Bind(part, wall.Stack.Bands[at].Material, at, wall.Stack.Bands[at].Thickness);
            }
        Bind(RoomParts.Post, storey.Post);
        Bind(RoomParts.Field, storey.Surface?.Field);

        var windows = storey.Windows ?? new WindowStyle();
        return new StoreyStyleSaveRequest(
            Name: name,
            Clear: storey.Clear,
            BorderWidth: storey.Surface?.BorderWidth ?? 1,
            InlayInset: storey.Surface?.InlayInset ?? 2,
            Windows: WindowDto(windows),
            Courses: courses);
    }

    // ── the houses ────────────────────────────────────────────────────────────────────────────────────
    private async Task<(int Added, int Updated)> SeedHousesAsync(StyleIds bound, CancellationToken ct)
    {
        var storeyIds = await SeedStoreysAsync(bound, ct);
        var existing = (await rooms.ListAsync(ct))
            .GroupBy(room => room.Name)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        int added = 0, updated = 0;
        foreach (var house in HousePresets.All)
        {
            var request = RequestFor(house, bound.ByName) with
            {
                StoreyStack = [.. Enumerable.Range(0, house.Style.Storeys.Count)
                    .Where(level => storeyIds.ContainsKey(StoreyName(house, level)))
                    .Select(level => new RoomStoreyDto(
                        storeyIds[StoreyName(house, level)], house.Style.Storeys[level].Clear))],
            };
            if (existing.TryGetValue(house.Name, out var row))
            {
                await rooms.UpdateAsync(
                    row.Id, RoomStyleLibrary.RowOf(request), RoomStyleLibrary.CourseRowsOf(request),
                    RoomStyleLibrary.StoreyRowsOf(request), ct);
                updated++;
                continue;
            }
            await rooms.CreateAsync(
                RoomStyleLibrary.RowOf(request), RoomStyleLibrary.CourseRowsOf(request),
                RoomStyleLibrary.StoreyRowsOf(request), ct);
            added++;
        }
        return (added, updated);
    }

    /// <summary>One preset as the save request the library stores — the same value the composer would have been
    /// handed by an editor, so a seeded row is a row an author could have made.</summary>
    private static RoomStyleSaveRequest RequestFor(HousePresets.House house, IReadOnlyDictionary<string, long> ids)
    {
        var style = house.Style;
        var courses = new List<RoomCourseDto>();

        void Bind(string part, TerrainMaterial? material, int ordinal = 0, int height = 1)
        {
            if (material is null) return;
            var key = $"{house.Name} · {part}";
            if (ids.TryGetValue(key, out var id)) courses.Add(new RoomCourseDto(PartOf(part), ordinal, id, height));
        }

        BindStack(RoomParts.Floor, style.Foundation.Plate);
        BindStack(RoomParts.Wall, style.Wall);
        Bind(RoomParts.Roof, style.Roof.Body);
        Bind(RoomParts.Verge, style.Roof.Verge);
        Bind(RoomParts.Sill, style.Foundation.Footing);
        Bind(RoomParts.Post, style.Post);
        Bind(RoomParts.Gable, style.Roof.Gable);
        Bind(RoomParts.Field, style.Foundation.Surface.Field);
        Bind(RoomParts.Border, style.Foundation.Surface.Border);
        Bind(RoomParts.Inlay, style.Foundation.Surface.Inlay);

        void BindStack(string part, RoomPart stack)
        {
            if (stack.Stack.Bands.Count <= 1) { Bind(part, stack.At(0).Material); return; }
            for (var at = 0; at < stack.Stack.Bands.Count; at++)
                Bind($"{part} {at + 1}", stack.Stack.Bands[at].Material, at, stack.Stack.Bands[at].Thickness);
        }

        var windows = style.Windows;
        return new RoomStyleSaveRequest(
            Name: house.Name,
            FloorDepth: style.Foundation.Depth,
            WallHeight: Math.Max(1, style.Wall.Extent),
            RoofForm: RoofForms.Canonical(NameOf(style.Roof.Form)),
            Pitch: style.Roof.Pitch,
            Overhang: style.Roof.Overhang,
            RoofHole: style.Roof.Hole,
            RidgeCap: style.Roof.RidgeCap,
            Storeys: Math.Max(1, style.Storeys.Count),
            StoreyClear: style.Storeys.Count > 0 ? style.Storeys[0].Clear : 0,
            Door: DoorMaterials.Slug(style.Doorway.Door),
            DoorHeight: style.Doorway.Height,
            BorderWidth: style.Foundation.Surface.BorderWidth,
            InlayInset: style.Foundation.Surface.InlayInset,
            Windows: WindowDto(windows),
            Porch: style.Porch is { } porch
                ? new RoomPorchDto(porch.Depth, porch.Inset, PorchEdges.Canonical(NameOf(porch.Edge)),
                                   RoofForms.Canonical(NameOf(porch.Roof)), porch.RailBlock)
                : null,
            Courses: courses,
            RoofStyleId: null,
            PorchStyleId: null,
            StoreyStack: [],
            Beams: new RoomBeamDto(style.Beams.Block, style.Beams.Data, style.Beams.Reach),
            RoofSlab: style.Roof.Slab,
            RoofSlabData: style.Roof.SlabData,
            GableWindows: WindowDto(style.Roof.GableWindows),
            DoorHead: new RoomDoorHeadDto(
                NameOf(style.Doorway.Head.Form), style.Doorway.Head.Block,
                NameOf(style.Doorway.Head.Fill), style.Doorway.Head.FillBlock, style.Doorway.Head.FillData));
    }

    // ── what the store could not hold ─────────────────────────────────────────────────────────────────
    /// <summary>
    /// Compose every seeded room style back out of the library and report where it differs from the preset it
    /// was seeded from.
    ///
    /// <para>A seeder is only as good as the round trip under it, and a row model always lags the stamper by
    /// however many knobs were added since: a preset that stores cleanly today grows a beam or a slab roof
    /// tomorrow and starts arriving as a quieter building than it left. Rather than trust that the two sides
    /// agree, this asks — and names each field that came back different, so the gap is a list rather than a
    /// suspicion.</para>
    /// </summary>
    public async Task<List<(string House, List<string> Lost)>> VerifyAsync(CancellationToken ct = default)
    {
        var library = new RoomStyleLibrary(rooms, parts, styles);
        var stored = (await rooms.ListAsync(ct))
            .GroupBy(room => room.Name)
            .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.OrdinalIgnoreCase);

        var report = new List<(string, List<string>)>();
        foreach (var house in HousePresets.All)
        {
            if (!stored.TryGetValue(house.Name, out var id)) { report.Add((house.Name, ["not stored"])); continue; }
            if (await library.ComposeAsync(id, ct) is not { } back) { report.Add((house.Name, ["unreadable"])); continue; }
            report.Add((house.Name, Differences(house.Style, back)));
        }
        return report;
    }

    /// <summary>The named fields on which a composed style differs from the preset. Spelled out one by one for
    /// the reason <see cref="HouseStyle.Equals(HouseStyle)"/> is: a difference the comparison does not name is
    /// a difference nobody goes and fixes.</summary>
    private static List<string> Differences(HouseStyle preset, HouseStyle back)
    {
        var lost = new List<string>();
        void Check(string what, object? mine, object? theirs)
        {
            if (!Equals(mine, theirs)) lost.Add(what);
        }

        Check("wall", preset.Wall, back.Wall);
        Check("floor", preset.Foundation.Plate, back.Foundation.Plate);
        Check("roof", preset.Roof.Body, back.Roof.Body);
        Check("verge", preset.Roof.Verge, back.Roof.Verge);
        Check("sill", preset.Foundation.Footing, back.Foundation.Footing);
        Check("post", preset.Post, back.Post);
        Check("gable", preset.Roof.Gable, back.Roof.Gable);
        Check("surface", preset.Foundation.Surface, back.Foundation.Surface);
        Check("form", preset.Roof.Form, back.Roof.Form);
        Check("pitch", preset.Roof.Pitch, back.Roof.Pitch);
        Check("overhang", preset.Roof.Overhang, back.Roof.Overhang);
        Check("ridgeCap", preset.Roof.RidgeCap, back.Roof.RidgeCap);
        Check("roofHole", preset.Roof.Hole, back.Roof.Hole);
        Check("roofSlab", preset.Roof.Slab, back.Roof.Slab);
        Check("roofSlabData", preset.Roof.SlabData, back.Roof.SlabData);
        Check("beams", preset.Beams, back.Beams);
        Check("windows", preset.Windows, back.Windows);
        Check("gableWindows", preset.Roof.GableWindows, back.Roof.GableWindows);
        Check("doorHead", preset.Doorway.Head, back.Doorway.Head);
        Check("door", preset.Doorway.Door, back.Doorway.Door);
        Check("doorWidth", preset.Doorway.Width, back.Doorway.Width);
        Check("doorHeight", preset.Doorway.Height, back.Doorway.Height);
        Check("porch", preset.Porch, back.Porch);

        // The stack storey by storey, not just how many there are. A room style stores a count and one clear,
        // so a building whose storeys differ from each other comes back as the same storey repeated — the
        // right number of rooms and the wrong rooms, which a count would call equal.
        var mine = preset.Levels;
        var theirs = back.Levels;
        if (mine.Count != theirs.Count) lost.Add($"storeys ({mine.Count} became {theirs.Count})");
        else
            for (var at = 0; at < mine.Count; at++)
            {
                if (mine[at].Clear != theirs[at].Clear) lost.Add($"storey {at + 1} clear");
                // Course for course rather than part for part: a storey's wall extent is ignored by the
                // stamper — its height is the storey's clear — so the store rebuilding it at the clear is the
                // model working, not the preset losing anything.
                if (!CoursesMatch(mine[at].Wall, theirs[at].Wall)) lost.Add($"storey {at + 1} wall");
                if (!Equals(mine[at].Post, theirs[at].Post)) lost.Add($"storey {at + 1} post");
                if (!Equals(mine[at].Windows, theirs[at].Windows)) lost.Add($"storey {at + 1} windows");
                if (!Equals(mine[at].Surface, theirs[at].Surface)) lost.Add($"storey {at + 1} floor");
            }
        return lost;
    }

    /// <summary>Whether two storey walls lay the same courses. Their extents are not compared: a storey's wall
    /// takes its height from the storey's clear, so the one the store rebuilds carries the clear whatever the
    /// preset happened to write.</summary>
    private static bool CoursesMatch(RoomPart? mine, RoomPart? theirs)
        => mine is null || theirs is null
            ? ReferenceEquals(mine, theirs)
            : mine.Stack.Bands.SequenceEqual(theirs.Stack.Bands);

    /// <summary>The stored part a course name belongs to — a stack's courses are named with their position and
    /// all bind the same part.</summary>
    private static string PartOf(string name) => name.Split(' ')[0];

    private static string NameOf(RoofForm form) => form switch
    {
        RoofForm.Flat => RoofForms.Flat,
        RoofForm.Hip => RoofForms.Hip,
        RoofForm.Gambrel => RoofForms.Gambrel,
        RoofForm.Shed => RoofForms.Shed,
        RoofForm.Saltbox => RoofForms.Saltbox,
        _ => RoofForms.Gable,
    };

    /// <summary>A window as the wire carries it, host band included.</summary>
    private static RoomWindowDto WindowDto(WindowStyle windows) => new(
        WindowForms.Canonical(NameOf(windows.Form)), windows.Block, windows.Data,
        windows.Sill, windows.Width, windows.Height, windows.Spacing,
        windows.HostBlock, windows.HostData);

    private static string NameOf(WindowForm form) => form switch
    {
        WindowForm.StairLattice => WindowForms.StairLattice,
        WindowForm.SlabBanded => WindowForms.SlabBanded,
        WindowForm.Pane => WindowForms.Pane,
        WindowForm.Open => WindowForms.Open,
        _ => WindowForms.None,
    };

    private static string NameOf(DoorHeadForm form)
        => form == DoorHeadForm.Arched ? DoorHeadForms.Arched : DoorHeadForms.None;

    private static string NameOf(DoorHeadFill fill)
        => fill == DoorHeadFill.Solid ? DoorHeadFills.Solid : DoorHeadFills.UpperSlab;

    private static string NameOf(RoomEdge? edge) => edge switch
    {
        RoomEdge.NegZ => PorchEdges.NegZ,
        RoomEdge.PosZ => PorchEdges.PosZ,
        RoomEdge.NegX => PorchEdges.NegX,
        RoomEdge.PosX => PorchEdges.PosX,
        _ => PorchEdges.Front,
    };
}
