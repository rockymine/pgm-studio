using System.Text;
using System.Text.Json;
using fNbt;
using static PgmStudio.Minecraft.Nbt;

namespace PgmStudio.Minecraft;

/// <summary>A wool monument whose surroundings we want to sample. <c>Center</c> is the monument
/// <c>&lt;block&gt;</c> coordinate from <c>xml_data.json</c> (<c>wools[].monuments[].location</c>) — the
/// cell a player places the objective wool into, which is air on a well-formed map (PGM marks the
/// wool "placed" when an objective wool block appears in this placement region).</summary>
public readonly record struct MonumentTarget(
    string MapSlug, string WoolId, string WoolColor, string MonumentId, string Team,
    int CenterX, int CenterY, int CenterZ);

/// <summary>One cell of a monument's surrounding slice — one row in <c>monument_slices.parquet</c>.
/// <c>(Dx,Dy,Dz)</c> is the offset from the monument block; the block itself is <c>(0,0,0)</c>.</summary>
public sealed record MonumentSliceCell(
    string MapSlug, string WoolId, string WoolColor, string MonumentId, string Team,
    int CenterX, int CenterY, int CenterZ,
    int Dx, int Dy, int Dz,
    int WorldX, int WorldY, int WorldZ,
    int BlockId, int BlockData, string BlockName,
    bool IsMonument, bool IsAir,
    string? TileEntityId, string? SignText, string? TileNbtJson,
    string? EntityIds, string? EntityNbtJson);

/// <summary>
/// Samples the block volume around each wool monument: a fixed <b>width 3 (x) × depth 3 (z) × height 5
/// (y)</b> box with the monument block at the centre — 1 block each horizontal direction, 2 above and
/// 2 below — so authors' monument framing is captured: the air placement cell, the bedrock usually set
/// directly below it, the labelling signs (their text is decoded), and any armour-stand / decorative
/// entities (full NBT preserved). Every monument always yields exactly <see cref="CellsPerMonument"/>
/// (45) cells; absent cells are reported as air so the slice is a regular tensor.
///
/// Sibling of <see cref="FeatureExtractors"/>: pure decode over the <c>AnvilRegion</c> stream + chunk
/// tile-entity / entity NBT, no parquet/DB dependency. Targets come from <c>xml_data.json</c>.
/// </summary>
public static class MonumentSliceExtractor
{
    public const int Width = 3, Depth = 3, Height = 5;          // x, z, y
    public const int CellsPerMonument = Width * Depth * Height; // 45

    // How far an entity's body/head rises above its feet (Pos), in blocks. A full-size armour stand
    // is ~2 tall, so a wool-indicator stand resting on the floor below the monument still reaches the
    // slice with the wool on its head — it's associated by this vertical reach, not just its feet cell.
    public const int EntityReach = 2;

    // Offsets relative to the monument block: ±1 horizontally, ±2 vertically. Emitted top-down so a
    // dump reads like looking at the column from above.
    private static readonly int[] DyOrder = [2, 1, 0, -1, -2];
    private static readonly int[] DzOrder = [-1, 0, 1];
    private static readonly int[] DxOrder = [-1, 0, 1];

    /// <summary>Extract every monument's slice from one map's chunks. The chunks are scanned once;
    /// blocks, tile entities and entities falling inside any slice are indexed by world coordinate.</summary>
    public static List<MonumentSliceCell> Extract(
        IEnumerable<AnvilRegion.Chunk> chunks, IReadOnlyList<MonumentTarget> monuments)
    {
        // Cells we care about, so the single chunk pass can skip everything else.
        var needed = new HashSet<(int, int, int)>();
        var footprint = new HashSet<(int, int)>();   // monument columns (±1 x/z), for the entity prefilter
        foreach (var m in monuments)
            foreach (var dz in DzOrder)
                foreach (var dx in DxOrder)
                {
                    footprint.Add((m.CenterX + dx, m.CenterZ + dz));
                    foreach (var dy in DyOrder) needed.Add((m.CenterX + dx, m.CenterY + dy, m.CenterZ + dz));
                }

        var (blockMap, tileList, entityList) = RegionScan.Read(chunks, (x, y, z) => needed.Contains((x, y, z)));
        var blocks = blockMap;
        var tiles = new Dictionary<(int, int, int), NbtCompound>();
        foreach (var (x, y, z, te) in tileList) tiles[(x, y, z)] = te;   // one tile entity per cell
        var candidates = entityList                                       // entities over a monument column
            .Where(e => footprint.Contains((e.Fx, e.Fz)))
            .Select(e => (e.Fx, e.Fy, e.Fz, Nbt: e.En)).ToList();

        // Associate each candidate entity to the in-slice cell its [feet, feet+EntityReach] span
        // covers nearest the monument block — so a wool-indicator armour stand standing below the
        // monument lands on the cell its head reaches (ties resolve to the lower cell).
        var entitiesByCell = new Dictionary<(string, int, int, int), List<NbtCompound>>();
        foreach (var m in monuments)
            foreach (var (fx, fy, fz, en) in candidates)
            {
                if (Math.Abs(fx - m.CenterX) > 1 || Math.Abs(fz - m.CenterZ) > 1) continue;
                int? attachY = null;
                for (var y = (int)Math.Floor(fy); y <= (int)Math.Floor(fy + EntityReach); y++)
                {
                    if (y < m.CenterY - 2 || y > m.CenterY + 2) continue;
                    if (attachY is null || Math.Abs(y - m.CenterY) < Math.Abs(attachY.Value - m.CenterY)) attachY = y;
                }
                if (attachY is null) continue;
                var key = (m.MonumentId, fx, attachY.Value, fz);
                (entitiesByCell.TryGetValue(key, out var list) ? list : entitiesByCell[key] = []).Add(en);
            }

        var rows = new List<MonumentSliceCell>(monuments.Count * CellsPerMonument);
        foreach (var m in monuments)
            foreach (var dy in DyOrder)
                foreach (var dz in DzOrder)
                    foreach (var dx in DxOrder)
                    {
                        var (wx, wy, wz) = (m.CenterX + dx, m.CenterY + dy, m.CenterZ + dz);
                        var (id, data) = blocks.GetValueOrDefault((wx, wy, wz), (0, 0));

                        string? tileId = null, signText = null, tileJson = null;
                        if (tiles.TryGetValue((wx, wy, wz), out var te))
                        {
                            tileId = Str(te.Get("id"));
                            tileJson = NbtToJson(te);
                            if (tileId == "Sign") signText = ReadSignText(te);
                        }

                        string? entityIds = null, entityJson = null;
                        if (entitiesByCell.TryGetValue((m.MonumentId, wx, wy, wz), out var ents) && ents.Count > 0)
                        {
                            entityIds = string.Join(",", ents.Select(e => Str(e.Get("id")) ?? "?"));
                            entityJson = JsonSerializer.Serialize(ents.Select(NbtToObj).ToList());
                        }

                        rows.Add(new MonumentSliceCell(
                            m.MapSlug, m.WoolId, m.WoolColor, m.MonumentId, m.Team,
                            m.CenterX, m.CenterY, m.CenterZ,
                            dx, dy, dz, wx, wy, wz,
                            id, data, BlockColors.Name(id, data),
                            IsMonument: dx == 0 && dy == 0 && dz == 0, IsAir: id == 0,
                            tileId, signText, tileJson, entityIds, entityJson));
                    }
        return rows;
    }

    /// <summary>Decode a sign's four text lines (1.8 JSON text components) into newline-joined plain
    /// text — this is where authors write the wool colour (e.g. "Green Wool"). Legacy raw strings pass
    /// through unchanged.</summary>
    public static string ReadSignText(NbtCompound sign)
    {
        var lines = new string[4];
        for (var i = 0; i < 4; i++)
            lines[i] = PlainTextComponent(Str(sign.Get($"Text{i + 1}")) ?? "");
        return string.Join("\n", lines);
    }

    private static string PlainTextComponent(string raw)
    {
        raw = raw.Trim();
        if (raw.Length == 0 || (raw[0] != '{' && raw[0] != '[' && raw[0] != '"')) return raw;   // legacy / plain
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var sb = new StringBuilder();
            CollectText(doc.RootElement, sb);
            return sb.ToString();
        }
        catch { return raw; }
    }

    private static void CollectText(JsonElement el, StringBuilder sb)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.String: sb.Append(el.GetString()); break;
            case JsonValueKind.Array: foreach (var c in el.EnumerateArray()) CollectText(c, sb); break;
            case JsonValueKind.Object:
                if (el.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String) sb.Append(t.GetString());
                if (el.TryGetProperty("extra", out var ex)) CollectText(ex, sb);
                break;
        }
    }

    // --- NBT → JSON (preserves the full tile-entity / entity payload as a string column) ---

    private static string NbtToJson(NbtTag tag) => JsonSerializer.Serialize(NbtToObj(tag));

    private static object? NbtToObj(NbtTag tag) => tag switch
    {
        NbtByte b => (int)b.Value,
        NbtShort s => (int)s.Value,
        NbtInt i => i.Value,
        NbtLong l => l.Value,
        NbtFloat f => f.Value,
        NbtDouble d => d.Value,
        NbtString s => s.Value,
        NbtByteArray ba => ba.Value.Select(x => (int)x).ToList(),
        NbtIntArray ia => ia.Value.ToList(),
        NbtList list => list.Select(NbtToObj).ToList(),
        NbtCompound comp => comp.ToDictionary(c => c.Name ?? "", NbtToObj),
        _ => null,
    };

}
