using System.Text.Json;
using System.Text.Json.Nodes;
using PgmStudio.Contracts;
using PgmStudio.Domain;
using PgmStudio.Data.Schema;
using PgmStudio.Data.Theme;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Minecraft.Houses;
using PgmStudio.Minecraft.Library;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Services;

/// <summary>
/// Puts the seed folder (<see cref="SeedFolder"/>) into a library: its patterns, themes, houses, boulders and
/// copied trees, the roofs, storeys and porches its houses are built of, a template tree per species and a flat
/// biome per biome. The last two are computed from the tables they name, so they stay code.
///
/// <para><b>A slot holds a block or a pattern.</b> A material that is one block is written into the slot that
/// lays it; only a material mixing blocks, or tinting one by team, is a pattern, and every such material a seeded
/// house or theme lays is one of the folder's patterns.</para>
///
/// <para><b>Idempotent, and matched by what a row holds.</b> A pattern, roof, storey or porch already holding the
/// seeded content is that row, and takes the seeded name; a pattern found by its name instead takes the seeded
/// content. A house and a theme are matched by name. Each keeps its id — which is what everything binding it
/// depends on — and nothing is ever deleted.</para>
///
/// <para>It is the inverse of <see cref="RoomStyleLibrary.Compose"/> and lives beside it for that reason: one
/// of them turns rows into a building and the other a building into rows. <see cref="VerifyAsync"/> composes
/// each seeded house back and reports what the store could not hold.</para>
/// </summary>
public sealed class LibrarySeed(ThemeStore styles, RoomStyleStore rooms, HousePartStore parts,
                               PropStyleStore props)
{
    /// <summary>What one run did, so a caller can say so rather than guessing from silence.</summary>
    public readonly record struct Tally(
        int PatternsAdded, int PatternsUpdated, int PartsAdded, int PartsUpdated, int HousesAdded, int HousesUpdated,
        int ThemesAdded, int ThemesUpdated, int RecipesAdded, int RecipesUpdated);

    /// <summary>Seed everything: the patterns first, since everything else binds them by id.</summary>
    public async Task<Tally> SeedAsync(CancellationToken ct = default)
    {
        var patterns = await SeedPatternsAsync(ct);
        var built = await SeedPartsAsync(patterns, ct);
        var houses = await SeedHousesAsync(patterns, built.Storeys, ct);
        var themes = await SeedThemesAsync(patterns, ct);
        var recipes = await SeedRecipesAsync(ct);
        await SeedBiomesAsync(ct);
        return new Tally(
            patterns.Added, patterns.Updated, built.Added, built.Updated, houses.Added, houses.Updated,
            themes.Added, themes.Updated, recipes.Added, recipes.Updated);
    }

    // ── the patterns ──────────────────────────────────────────────────────────────────────────────────
    /// <summary>The library's id for each seeded pattern, by the content it holds.</summary>
    private sealed class Patterns
    {
        public Dictionary<string, long> ByContent { get; } = new(StringComparer.Ordinal);
        public Dictionary<long, TerrainMaterial> ById { get; } = [];
        public int Added, Updated;

        /// <summary>What a slot laying <paramref name="material"/> binds: its block, or the seeded pattern
        /// holding it. A material that is neither is a fault in the folder, which the seed tests rule out.</summary>
        public (long StyleId, SlotBlockDto? Block) Fill(TerrainMaterial material)
            => Slots.AsBlock(material) is { } block
                ? (0, block)
                : ByContent.TryGetValue(TerrainThemeJson.Serialize(material), out var id)
                    ? (id, null)
                    : throw new InvalidDataException(
                        $"the seed folder lays a pattern it does not name: {TerrainThemeJson.Serialize(material)}");
    }

    private async Task<Patterns> SeedPatternsAsync(CancellationToken ct)
    {
        var stored = await styles.ListStylesAsync(ct: ct);
        var byContent = stored.GroupBy(row => ThemeLibrary.ContentOf(row.Params), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.MinBy(row => row.Id)!, StringComparer.Ordinal);
        var byName = stored.GroupBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.MinBy(row => row.Id)!, StringComparer.OrdinalIgnoreCase);

        var patterns = new Patterns();
        foreach (var (name, material) in SeedFolder.Patterns)
        {
            var content = TerrainThemeJson.Serialize(material);
            var kind = TerrainThemeComposer.KindOf(material);
            if (byContent.TryGetValue(content, out var held) || byName.TryGetValue(name, out held))
            {
                if (held.Name != name || held.Kind != kind || held.Params != content)
                {
                    await styles.UpdateStyleAsync(held.Id, name, kind, content, ct);
                    patterns.Updated++;
                }
                patterns.ByContent[content] = held.Id;
                patterns.ById[held.Id] = material;
                continue;
            }
            var id = await styles.CreateStyleAsync(new StyleRow { Name = name, Kind = kind, Params = content }, ct);
            patterns.ByContent[content] = id;
            patterns.ById[id] = material;
            patterns.Added++;
        }
        return patterns;
    }

    // ── the roofs, storeys and porches ────────────────────────────────────────────────────────────────
    /// <summary>The parts the houses are built of, each stored once however many houses share it, and named
    /// for what it is (<see cref="PartNames"/>). A house binds its storeys by id; its roof and porch are its
    /// own columns, and the rows here are what the roof and porch libraries offer.</summary>
    private async Task<(int Added, int Updated, Dictionary<string, long> Storeys)> SeedPartsAsync(
        Patterns patterns, CancellationToken ct)
    {
        int added = 0, updated = 0;

        var roofs = Distinct(SeedFolder.Houses.Select(house => RoofRequest(house.Style, patterns)),
            request => PartKey.Of(HousePartLibrary.RowOf(request), HousePartLibrary.RoofCourseRowsOf(request)),
            (request, name) => request with { Name = name }, roof => PartNames.Roof(roof, patterns.ById));
        var storedRoofs = await parts.ListRoofsAsync(ct);
        var roofCourses = (await parts.GetAllRoofCoursesAsync(ct)).ToLookup(course => course.RoofStyleId);
        var roofByKey = storedRoofs.GroupBy(row => PartKey.Of(row, roofCourses[row.Id]), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.MinBy(row => row.Id)!, StringComparer.Ordinal);
        foreach (var (key, request) in roofs)
        {
            if (roofByKey.TryGetValue(key, out var held))
            {
                if (held.Name == request.Name) continue;
                await parts.UpdateRoofAsync(held.Id, HousePartLibrary.RowOf(request),
                    HousePartLibrary.RoofCourseRowsOf(request), ct);
                updated++;
                continue;
            }
            await parts.CreateRoofAsync(HousePartLibrary.RowOf(request), HousePartLibrary.RoofCourseRowsOf(request), ct);
            added++;
        }

        var storeys = Distinct(
            SeedFolder.Houses.SelectMany(house => Enumerable.Range(0, house.Style.Storeys.Count)
                .Select(level => StoreyRequest(house.Style, level, patterns))),
            request => PartKey.Of(HousePartLibrary.RowOf(request), HousePartLibrary.StoreyCourseRowsOf(request)),
            (request, name) => request with { Name = name }, storey => PartNames.Storey(storey, patterns.ById));
        var storedStoreys = await parts.ListStoreysAsync(ct);
        var storeyCourses = (await parts.GetAllStoreyCoursesAsync(ct)).ToLookup(course => course.StoreyStyleId);
        var storeyByKey = storedStoreys.GroupBy(row => PartKey.Of(row, storeyCourses[row.Id]), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.MinBy(row => row.Id)!, StringComparer.Ordinal);
        var storeyIds = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var (key, request) in storeys)
        {
            if (storeyByKey.TryGetValue(key, out var held))
            {
                storeyIds[key] = held.Id;
                if (held.Name == request.Name) continue;
                await parts.UpdateStoreyAsync(held.Id, HousePartLibrary.RowOf(request),
                    HousePartLibrary.StoreyCourseRowsOf(request), ct);
                updated++;
                continue;
            }
            storeyIds[key] = await parts.CreateStoreyAsync(
                HousePartLibrary.RowOf(request), HousePartLibrary.StoreyCourseRowsOf(request), ct);
            added++;
        }

        var porches = Distinct(
            SeedFolder.Houses.Where(house => house.Style.Porch is not null)
                .Select(house => PorchRequest(house.Style.Porch!)),
            request => PartKey.Of(HousePartLibrary.RowOf(request), []),
            (request, name) => request with { Name = name }, PartNames.Porch);
        var porchByKey = (await parts.ListPorchesAsync(ct))
            .GroupBy(row => PartKey.Of(row, []), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.MinBy(row => row.Id)!, StringComparer.Ordinal);
        foreach (var (key, request) in porches)
        {
            if (porchByKey.TryGetValue(key, out var held))
            {
                if (held.Name == request.Name) continue;
                await parts.UpdatePorchAsync(held.Id, HousePartLibrary.RowOf(request), ct);
                updated++;
                continue;
            }
            await parts.CreatePorchAsync(HousePartLibrary.RowOf(request), ct);
            added++;
        }
        return (added, updated, storeyIds);
    }

    /// <summary>The distinct parts of a list, in the order each first appears, each under the name it describes
    /// itself by (<see cref="PartNames"/>). Where two distinct parts describe themselves alike, each takes the
    /// fewest words for what tells it apart, and a count only where nothing stated does.</summary>
    private static List<(string Key, T Part)> Distinct<T>(
        IEnumerable<T> all, Func<T, string> keyOf, Func<T, string, T> named, Func<T, PartName> describe)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var distinct = new List<(string Key, T Part, PartName Name)>();
        foreach (var part in all)
        {
            var key = keyOf(part);
            if (seen.Add(key)) distinct.Add((key, part, describe(part)));
        }

        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<(string, T)>();
        var alike = distinct.ToLookup(entry => entry.Name.Base, StringComparer.Ordinal);
        foreach (var (key, part, name) in distinct)
        {
            // An aspect is named only while another part still reads the same, and only where it tells this
            // one apart from at least one of them.
            var rivals = alike[name.Base].Where(other => other.Key != key).Select(other => other.Name).ToList();
            var words = new List<string> { name.Base };
            foreach (var (aspect, word) in name.Qualifiers)
            {
                if (rivals.Count == 0) break;
                if (rivals.All(rival => rival.Word(aspect) == word)) continue;
                words.Add(word);
                rivals = [.. rivals.Where(rival => rival.Word(aspect) == word)];
            }
            var described = string.Join('-', words);
            var unique = PatternNames.Unique(described, taken);
            taken.Add(unique);
            result.Add((key, named(part, unique)));
        }
        return result;
    }

    /// <summary>A house's roof as the request the library stores.</summary>
    private static RoofStyleSaveRequest RoofRequest(HouseStyle style, Patterns patterns)
    {
        var roof = style.Roof;
        var courses = new List<RoomCourseDto>();
        void Bind(string part, TerrainMaterial? material)
        {
            if (material is null) return;
            var (styleId, block) = patterns.Fill(material);
            courses.Add(new RoomCourseDto(part, 0, styleId, block, 1));
        }
        Bind(RoomParts.Roof, roof.Body);
        Bind(RoomParts.Verge, roof.Verge);
        Bind(RoomParts.Gable, roof.Gable);

        return new RoofStyleSaveRequest(
            Name: "", Form: RoofForms.Canonical(NameOf(roof.Form)), Pitch: roof.Pitch, Overhang: roof.Overhang,
            RoofHole: roof.Hole, RidgeCap: roof.RidgeCap, Courses: courses,
            RoofSlab: roof.Slab, RoofSlabData: roof.SlabData, RoofStair: roof.Stair, RoofWear: roof.Wear);
    }

    /// <summary>One storey as the request the library stores — the <em>resolved</em> storey, not the declared
    /// one: a storey that names no wall or windows of its own takes the building's, and what a storey style
    /// stores is what that storey actually is. Its deck is the declared one: unbound, the store gives a storey
    /// the floor's own top material, which is exactly what a storey naming no deck stands on.</summary>
    private static StoreyStyleSaveRequest StoreyRequest(HouseStyle style, int level, Patterns patterns)
    {
        var storey = style.Levels[level];
        var courses = new List<RoomCourseDto>();
        void Bind(string part, TerrainMaterial? material, int ordinal = 0, int height = 1)
        {
            if (material is null) return;
            var (styleId, block) = patterns.Fill(material);
            courses.Add(new RoomCourseDto(part, ordinal, styleId, block, height));
        }

        if (storey.Wall is { } wall)
            for (var at = 0; at < wall.Stack.Bands.Count; at++)
                Bind(RoomParts.Wall, wall.Stack.Bands[at].Material, at, wall.Stack.Bands[at].Thickness);
        Bind(RoomParts.Post, storey.Post);
        Bind(RoomParts.Field, storey.Surface?.Field);
        Bind(RoomParts.Deck, style.Storeys[level].Deck);

        return new StoreyStyleSaveRequest(
            Name: "",
            Clear: storey.Clear,
            BorderWidth: storey.Surface?.BorderWidth ?? 1,
            InlayInset: storey.Surface?.InlayInset ?? 2,
            Windows: WindowDto(storey.Windows ?? new WindowStyle()),
            Courses: courses);
    }

    private static PorchStyleSaveRequest PorchRequest(PorchStyle porch) => new(
        "", porch.Depth, porch.Inset, PorchEdges.Canonical(NameOf(porch.Edge)),
        RoofForms.Canonical(NameOf(porch.Roof)), porch.RailBlock);

    // ── the houses ────────────────────────────────────────────────────────────────────────────────────
    private async Task<(int Added, int Updated)> SeedHousesAsync(
        Patterns patterns, IReadOnlyDictionary<string, long> storeyIds, CancellationToken ct)
    {
        var existing = (await rooms.ListAsync(ct))
            .GroupBy(room => room.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        int added = 0, updated = 0;
        foreach (var (house, style) in SeedFolder.Houses)
        {
            var request = HouseRequest(house, style, patterns) with
            {
                StoreyStack = [.. Enumerable.Range(0, style.Storeys.Count).Select(level =>
                {
                    var storey = StoreyRequest(style, level, patterns);
                    var key = PartKey.Of(HousePartLibrary.RowOf(storey), HousePartLibrary.StoreyCourseRowsOf(storey));
                    return new RoomStoreyDto(storeyIds[key], style.Storeys[level].Clear);
                })],
            };
            if (existing.TryGetValue(house, out var row))
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

    /// <summary>One house as the save request the library stores — the same value the composer would have been
    /// handed by an editor, so a seeded row is a row an author could have made.</summary>
    private static RoomStyleSaveRequest HouseRequest(string house, HouseStyle style, Patterns patterns)
    {
        var courses = new List<RoomCourseDto>();

        void Bind(string part, TerrainMaterial? material, int ordinal = 0, int height = 1)
        {
            if (material is null) return;
            var (styleId, block) = patterns.Fill(material);
            courses.Add(new RoomCourseDto(part, ordinal, styleId, block, height));
        }

        void BindStack(string part, RoomPart stack)
        {
            if (stack.Stack.Bands.Count <= 1)
            {
                Bind(part, stack.At(0).Material, height: stack.Stack.Bands.Count == 1 ? stack.Stack.Bands[0].Thickness : 1);
                return;
            }
            for (var at = 0; at < stack.Stack.Bands.Count; at++)
                Bind(part, stack.Stack.Bands[at].Material, at, stack.Stack.Bands[at].Thickness);
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
        Bind(RoomParts.Canopy, style.Porch?.Canopy);

        var windows = style.Windows;
        return new RoomStyleSaveRequest(
            Name: house,
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
            DoorWidth: style.Doorway.Width,
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
                NameOf(style.Doorway.Head.Fill), style.Doorway.Head.FillBlock, style.Doorway.Head.FillData),
            RoofStair: style.Roof.Stair,
            RoofWear: style.Roof.Wear,
            Front: PorchEdges.Canonical(NameOf(style.Front)));
    }

    // ── the finishes ──────────────────────────────────────────────────────────────────────────────────
    /// <summary>Each seeded finish as a theme row filling each bucket with a block or a pattern, keyed by name.
    /// A theme composed of nothing is a tab that opens on a sentence telling an author to start one, which is
    /// the hardest thing in the library to start from nothing.</summary>
    private async Task<(int Added, int Updated)> SeedThemesAsync(Patterns patterns, CancellationToken ct)
    {
        var existing = (await styles.ListThemesAsync(ct))
            .GroupBy(theme => theme.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.OrdinalIgnoreCase);

        int added = 0, updated = 0;
        foreach (var (name, theme) in SeedFolder.Themes)
        {
            var decomposed = TerrainThemeComposer.Decompose(theme);
            var buckets = new List<ThemeBucketRow>();
            foreach (var binding in decomposed.Buckets)
            {
                var row = new ThemeBucketRow
                {
                    Bucket = ThemeLibrary.FromBucket(binding.Bucket), Depth = binding.Depth, Enabled = binding.Enabled,
                };
                if (binding.MaterialJson is { } json)
                {
                    var (styleId, block) = patterns.Fill(TerrainThemeJson.DeserializeMaterial(json));
                    if (block is not null) (row.BlockId, row.BlockData, row.BlockLaid) = (block.Id, block.Data, block.Laid);
                    else row.StyleId = styleId;
                }
                buckets.Add(row);
            }

            var themeRow = new ThemeRow
            {
                Name = name,
                BedrockRelative = decomposed.BedrockRelative, BedrockValue = decomposed.BedrockValue,
                RimEdges = ThemeLibrary.FromRimEdges(decomposed.RimEdges),
                WallOnTerrainFaces = decomposed.WallOnTerrainFaces,
            };
            if (existing.TryGetValue(name, out var themeId))
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

    // ── the prop recipes ──────────────────────────────────────────────────────────────────────────────
    /// <summary>The recipes a click puts down: a template tree per species at its natural height, the folder's
    /// boulders and the trees cut out of the showcase world. Without them a studio opens two of its libraries on
    /// nothing, and a picker with no rows in it is a picker an author cannot use at all.
    ///
    /// <para>A template and a boulder are seeded where no row has the name, and a recipe an author has since
    /// retuned keeps their numbers, because the row is theirs once it exists. A copied tree is matched by its
    /// cut — the world it came from and the foot it stood on — so the folder's cut is what it holds.</para></summary>
    private async Task<(int Added, int Updated)> SeedRecipesAsync(CancellationToken ct)
    {
        int added = 0, updated = 0;
        var trees = await props.ListTreesAsync(ct);
        var treeNames = trees.Select(row => row.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var species in DressingPalette.Species)
        {
            if (treeNames.Contains(species.Name)) continue;
            await props.CreateTreeAsync(PropStyleLibrary.RowOf(new TreeStyleSaveRequest(
                species.Name, TreeForms.Template, species.Name, species.Height)), ct);
            added++;
        }

        var cut = SeedFolder.Trees;
        var byCut = trees
            .Where(row => row is { CutWorld.Length: > 0, CutX: not null, CutY: not null, CutZ: not null })
            .GroupBy(row => (World: Path.GetFileName(row.CutWorld!.TrimEnd('/')), X: row.CutX!.Value,
                             Y: row.CutY!.Value, Z: row.CutZ!.Value))
            .ToDictionary(group => group.Key, group => group.First());
        foreach (var tree in cut.Trees)
        {
            var seeded = PropStyleLibrary.RowOf(tree.Name, cut.World, tree.Foot, tree.Style);
            if (byCut.TryGetValue((cut.World, tree.Foot.X, tree.Foot.Y, tree.Foot.Z), out var held))
            {
                if (held.Name == seeded.Name && held.Body == seeded.Body && held.Species == seeded.Species
                    && held.CutBuilder == seeded.CutBuilder) continue;
                seeded.CutAt = held.CutAt;
                seeded.CutWorld = held.CutWorld;
                await props.UpdateTreeAsync(held.Id, seeded, ct);
                updated++;
                continue;
            }
            seeded.CutAt = DateTime.UtcNow;
            await props.CreateTreeAsync(seeded, ct);
            added++;
        }

        var rocks = (await props.ListBouldersAsync(ct))
            .Select(row => row.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, boulder) in SeedFolder.Boulders)
        {
            if (rocks.Contains(name)) continue;
            await props.CreateBoulderAsync(PropStyleLibrary.RowOf(name, boulder), ct);
            added++;
        }
        return (added, updated);
    }

    // ── the biome patterns ────────────────────────────────────────────────────────────────────────────
    /// <summary>One flat pattern per biome, so wanting a board that is simply desert is a pick rather
    /// than a document to write. Computed from the biome table, and idempotent by name like every other seed
    /// here, so a preset an author has since retuned keeps their numbers.</summary>
    private async Task SeedBiomesAsync(CancellationToken ct)
    {
        var named = (await styles.ListBiomesAsync(ct: ct))
            .Select(row => row.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var biome in PgmStudio.Minecraft.Palette.Biome.All)
        {
            if (named.Contains(biome.Name)) continue;
            await styles.CreateBiomeAsync(new BiomePatternRow
            {
                Name = biome.Name,
                Kind = BiomeKinds.Solid,
                Params = TerrainThemeJson.SerializeBiome(new SolidBiome(biome.Id)),
            }, ct);
        }
    }

    // ── what the store could not hold ─────────────────────────────────────────────────────────────────
    /// <summary>
    /// Compose every seeded room style back out of the library and report where it differs from the house it
    /// was seeded from. A row model lags the stamper by however many knobs were added since, so rather than
    /// trust that the two sides agree, this asks — and names each field that came back different.
    /// </summary>
    public async Task<List<(string House, List<string> Lost)>> VerifyAsync(CancellationToken ct = default)
    {
        var library = new RoomStyleLibrary(rooms, parts, styles);
        var stored = (await rooms.ListAsync(ct))
            .GroupBy(room => room.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.OrdinalIgnoreCase);

        var report = new List<(string, List<string>)>();
        foreach (var (house, style) in SeedFolder.Houses)
        {
            if (!stored.TryGetValue(house, out var id)) { report.Add((house, ["not stored"])); continue; }
            if (await library.ComposeAsync(id, ct) is not { } back) { report.Add((house, ["unreadable"])); continue; }
            report.Add((house, Differences(style, back)));
        }
        return report;
    }

    /// <summary>The named fields on which a composed style differs from the seeded one. Spelled out one by one
    /// for the reason <see cref="HouseStyle.Equals(HouseStyle)"/> is: a difference the comparison does not name
    /// is a difference nobody goes and fixes.</summary>
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
        Check("roofStair", preset.Roof.Stair, back.Roof.Stair);
        Check("roofWear", preset.Roof.Wear, back.Roof.Wear);
        Check("beams", preset.Beams, back.Beams);
        Check("windows", preset.Windows, back.Windows);
        Check("gableWindows", preset.Roof.GableWindows, back.Roof.GableWindows);
        Check("doorHead", preset.Doorway.Head, back.Doorway.Head);
        Check("door", preset.Doorway.Door, back.Doorway.Door);
        Check("doorWidth", preset.Doorway.Width, back.Doorway.Width);
        Check("doorHeight", preset.Doorway.Height, back.Doorway.Height);
        Check("porch", preset.Porch, back.Porch);
        Check("front", preset.Front, back.Front);

        // The stack storey by storey, not just how many there are: a building whose storeys differ from each
        // other must not come back as the same storey repeated.
        var mine = preset.Levels;
        var theirs = back.Levels;
        if (mine.Count != theirs.Count) lost.Add($"storeys ({mine.Count} became {theirs.Count})");
        else
            for (var at = 0; at < mine.Count; at++)
            {
                if (mine[at].Clear != theirs[at].Clear) lost.Add($"storey {at + 1} clear");
                // Course for course rather than part for part: a storey's wall extent is ignored by the
                // stamper — its height is the storey's clear.
                if (!CoursesMatch(mine[at].Wall, theirs[at].Wall)) lost.Add($"storey {at + 1} wall");
                if (!Equals(mine[at].Post, theirs[at].Post)) lost.Add($"storey {at + 1} post");
                if (!Equals(mine[at].Windows, theirs[at].Windows)) lost.Add($"storey {at + 1} windows");
                if (!Equals(mine[at].Surface, theirs[at].Surface)) lost.Add($"storey {at + 1} floor");
                if (!Equals(mine[at].Deck, theirs[at].Deck)) lost.Add($"storey {at + 1} deck");
            }
        return lost;
    }

    /// <summary>Whether two storey walls lay the same courses. Their extents are not compared: a storey's wall
    /// takes its height from the storey's clear.</summary>
    private static bool CoursesMatch(RoomPart? mine, RoomPart? theirs)
        => mine is null || theirs is null
            ? ReferenceEquals(mine, theirs)
            : mine.Stack.Bands.SequenceEqual(theirs.Stack.Bands);

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

    private static string NameOf(WindowForm form) => WindowFormWords.Of(form);

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

/// <summary>What a roof, a storey or a porch row holds, as one string two rows holding the same share: every
/// column but its id, name and creation time, and its courses in stack order with their owner left out.</summary>
internal static class PartKey
{
    private static readonly HashSet<string> Identity = new(StringComparer.Ordinal)
    {
        "Id", "Name", "CreatedAt", "RoofStyleId", "StoreyStyleId", "RoomStyleId",
    };

    public static string Of(object row, IEnumerable<object> courses)
    {
        var ordered = courses.Select(Columns)
            .OrderBy(course => course["Part"]?.GetValue<string>(), StringComparer.Ordinal)
            .ThenBy(course => course["Ordinal"]?.GetValue<int>())
            .ToList();
        return new JsonObject { ["row"] = Columns(row), ["courses"] = new JsonArray([.. ordered]) }.ToJsonString();
    }

    private static JsonObject Columns(object row)
    {
        var columns = JsonSerializer.SerializeToNode(row, row.GetType())!.AsObject();
        foreach (var name in Identity) columns.Remove(name);
        return columns;
    }
}

/// <summary>The name a part describes itself by: a <see cref="Base"/> naming what it is laid in and what it is,
/// and the <see cref="Qualifiers"/> a name takes where another part shares the base — each an aspect and the words
/// for this part's value of it.</summary>
internal sealed record PartName(string Base, IReadOnlyList<(string Aspect, string Word)> Qualifiers)
{
    public string? Word(string aspect) => Qualifiers.FirstOrDefault(qualifier => qualifier.Aspect == aspect).Word;
}

/// <summary>The names a seeded roof, storey and porch describe themselves by, in the words a pattern's name
/// uses: <c>spruce-planks-gable-roof</c>, <c>cobblestone-andesite-spruce-log-storey</c>,
/// <c>oak-fence-front-porch</c>, and <c>dark-oak-planks-gable-roof-pitch-2-spruce-planks-verge</c> where two roofs of
/// one wood differ in pitch and verge. Computed from the part, because the parts are cut out of the seeded houses
/// rather than stated.</summary>
internal static class PartNames
{
    public static PartName Roof(RoofStyleSaveRequest roof, IReadOnlyDictionary<long, TerrainMaterial> patterns) => new(
        $"{Laid(roof.Courses, RoomParts.Roof, patterns)}-{roof.Form}-roof",
        [
            ("pitch", $"pitch-{roof.Pitch}"),
            ("verge", $"{Laid(roof.Courses, RoomParts.Verge, patterns)}-verge"),
            ("gable", $"{Laid(roof.Courses, RoomParts.Gable, patterns)}-gables"),
            ("stair", roof.RoofStair >= 0 ? PatternNames.BlockWord(roof.RoofStair, 0) : "block-stepped"),
            ("slab", roof.RoofSlab >= 0 ? PatternNames.BlockWord(roof.RoofSlab, roof.RoofSlabData) : "unslabbed"),
            ("overhang", $"overhang-{roof.Overhang}"),
            ("ridge", roof.RidgeCap ? "ridge-capped" : "open-ridge"),
            ("hole", roof.RoofHole ? "holed" : "whole"),
            ("wear", $"worn-{Math.Round(roof.RoofWear * 100)}"),
        ]);

    public static PartName Storey(StoreyStyleSaveRequest storey, IReadOnlyDictionary<long, TerrainMaterial> patterns) => new(
        $"{Laid(storey.Courses, RoomParts.Wall, patterns)}-storey",
        [
            ("clear", $"{storey.Clear}-high"),
            ("post", $"{Laid(storey.Courses, RoomParts.Post, patterns)}-posts"),
            ("windows", storey.Windows.Form == WindowForms.None
                ? "windowless"
                : $"{PatternNames.BlockWord(storey.Windows.Block, storey.Windows.Data)}-{Kebab(storey.Windows.Form)}-windows"),
            ("windowSize", $"{storey.Windows.Width}x{storey.Windows.Height}-windows"),
            ("spacing", $"spaced-{storey.Windows.Spacing}"),
            ("sill", $"sill-{storey.Windows.Sill}"),
            ("host", storey.Windows.HostBlock >= 0
                ? $"set-in-{PatternNames.BlockWord(storey.Windows.HostBlock, storey.Windows.HostData)}"
                : "unset"),
            ("border", $"border-{storey.BorderWidth}"),
            ("inlay", $"inlay-{storey.InlayInset}"),
            ("field", $"{Laid(storey.Courses, RoomParts.Field, patterns)}-floor"),
            ("deck", $"{Laid(storey.Courses, RoomParts.Deck, patterns)}-deck"),
            ("wall", $"all-{Laid(storey.Courses, RoomParts.Wall, patterns, words: int.MaxValue)}"),
            ("courses", Heights(storey.Courses, RoomParts.Wall)),
        ]);

    public static PartName Porch(PorchStyleSaveRequest porch) => new(
        $"{PatternNames.BlockWord(porch.RailBlock, 0)}-{porch.Edge}-porch",
        [("depth", $"{porch.Depth}-deep"), ("inset", $"inset-{porch.Inset}"), ("roof", $"{porch.Roof}-roofed")]);

    /// <summary>How a part's courses stack, as their heights: <c>courses-3-1</c>, or <c>striped-12</c> past four.</summary>
    private static string Heights(IEnumerable<RoomCourseDto> courses, string part)
    {
        var heights = courses.Where(course => course.Part == part).OrderBy(course => course.Ordinal)
            .Select(course => course.Height).ToList();
        return heights.Count > 4 ? $"striped-{heights.Count}" : $"courses-{string.Join('-', heights)}";
    }

    /// <summary>A camel-cased wire word as a name writes it: <c>stairLattice</c> as <c>stair-lattice</c>.</summary>
    private static string Kebab(string word)
        => string.Concat(word.Select(letter => char.IsUpper(letter) ? $"-{char.ToLowerInvariant(letter)}" : $"{letter}"));

    /// <summary>What a part's courses are laid in, as up to <paramref name="words"/> block words.</summary>
    private static string Laid(
        IEnumerable<RoomCourseDto> courses, string part, IReadOnlyDictionary<long, TerrainMaterial> patterns,
        int words = 3)
    {
        var laid = courses.Where(course => course.Part == part)
            .OrderBy(course => course.Ordinal)
            .SelectMany(course => course.Block is { } block
                ? [PatternNames.BlockWord(block.Id, block.Data)]
                : patterns.TryGetValue(course.StyleId, out var material) ? PatternNames.BlockWords(material) : [])
            .Distinct()
            .Take(words)
            .ToList();
        return laid.Count == 0 ? "bare" : string.Join('-', laid);
    }
}
