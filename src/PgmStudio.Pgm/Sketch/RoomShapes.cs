using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Sketch;

/// <summary>
/// What a layout states about the ground under one play room, keyed in <see cref="SketchLayout.Rooms"/> by the
/// id of the shape the room is drawn as: a piece's id for its region, that id with <c>-building</c> after it
/// for the first wing of the house on it, and <c>-building-2</c> on for the rest. Where the room is belongs to the
/// intent; how the ground meets it belongs here.
/// Every field means what the same field means on a <see cref="SketchShape"/>.
/// </summary>
public sealed class RoomGround
{
    /// <summary>Where the room's base sits, in blocks.</summary>
    [JsonPropertyName("floor")] public double? Floor { get; set; }

    /// <summary>The room's stated top, over <see cref="Floor"/>.</summary>
    [JsonPropertyName("base_height")] public double? BaseHeight { get; set; }

    /// <summary>A top per corner, interpolated across the region.</summary>
    [JsonPropertyName("anchor_heights")] public double[]? AnchorHeights { get; set; }

    /// <summary>How the room takes part in its group's relief: <c>follow</c>, <c>hold</c> or <c>exclude</c>.
    /// Absent pins nothing.</summary>
    [WordSet(typeof(ReliefScopes))]
    [JsonPropertyName("relief_scope")] public string? ReliefScope { get; set; }

    /// <summary>Whether the author corrected the room's height, which a recompile then keeps.</summary>
    [JsonPropertyName("height_authored")] public bool? HeightAuthored { get; set; }

    /// <summary>The sides whose ground the room is level with, as edge words.</summary>
    [JsonPropertyName("doors")] public string[]? Doors { get; set; }

    /// <summary>The theme the room's ground paints with, by id into the layout's registry.</summary>
    [JsonPropertyName("theme")] public string? Theme { get; set; }

    /// <summary>One material the room's ground is made of, in place of a theme.</summary>
    [JsonPropertyName("material")] public JsonElement? Material { get; set; }

    /// <summary>Whether the record states nothing.</summary>
    [JsonIgnore]
    public bool IsEmpty => Floor is null && BaseHeight is null && AnchorHeights is null && ReliefScope is null
                           && HeightAuthored is null && Doors is null && Theme is null && Material is null;
}

/// <summary>
/// The play rooms a layout is read with: a region shape for every spawn and wool whose protection is one
/// rectangle, and a building shape for each that states a footprint, drawn from the intent and carrying what
/// <see cref="SketchLayout.Rooms"/> states about their ground. A stored layout holds no room shape;
/// <see cref="Project"/> adds them for a reader and <see cref="Fold"/> takes them back out for a store, so a
/// document read, edited and stored keeps the record and loses only the picture.
/// </summary>
public static class RoomShapes
{
    private static readonly JsonSerializerOptions Sparse = new(SketchLayout.Json)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>The layout as a reader sees it: any room shape it carries folded into its record first, then
    /// the intent's rooms drawn into its first layer. A layout with no layer draws none.</summary>
    public static string Project(string layoutJson, MapIntent intent)
    {
        var folded = Fold(layoutJson);
        if (JsonNode.Parse(folded) is not JsonObject root) return folded;
        if (root["layers"] is not JsonArray { Count: > 0 } layers
            || layers[0]?["layout"]?["shapes"] is not JsonArray shapes) return folded;

        var rooms = root["rooms"]?.Deserialize<Dictionary<string, RoomGround>>(SketchLayout.Json);
        var drawn = Of(intent, rooms).ToList();
        if (drawn.Count == 0) return folded;
        foreach (var shape in drawn) shapes.Add(JsonSerializer.SerializeToNode(shape, Sparse));
        return root.ToJsonString(SketchLayout.Json);
    }

    /// <summary>The layout as it is stored: every room shape taken out of every layer, and what each stated
    /// about its ground written into <see cref="SketchLayout.Rooms"/> under its id. A layout carrying no room
    /// shape is answered byte for byte as it came.</summary>
    public static string Fold(string layoutJson)
    {
        JsonNode? node;
        try { node = JsonNode.Parse(layoutJson); }
        catch (JsonException) { return layoutJson; }
        if (node is not JsonObject root || root["layers"] is not JsonArray layers) return layoutJson;

        var stated = new List<SketchShape>();
        foreach (var layer in layers)
        {
            if (layer?["layout"]?["shapes"] is not JsonArray shapes) continue;
            for (var at = shapes.Count - 1; at >= 0; at--)
            {
                if (shapes[at] is not JsonObject shape || shape["role"] is not JsonValue) continue;
                if (shape.Deserialize<SketchShape>(SketchLayout.Json) is { } read) stated.Insert(0, read);
                shapes.RemoveAt(at);
            }
        }
        if (stated.Count == 0) return layoutJson;

        var rooms = root["rooms"] as JsonObject ?? new JsonObject();
        foreach (var shape in stated)
        {
            var ground = Ground(shape);
            if (ground.IsEmpty) rooms.Remove(shape.Id);
            else rooms[shape.Id] = JsonSerializer.SerializeToNode(ground, Sparse);
        }
        if (rooms.Count > 0) root["rooms"] = rooms;
        else root.Remove("rooms");
        return root.ToJsonString(SketchLayout.Json);
    }

    /// <summary>The room shapes an intent draws, each carrying the record stated for its id.</summary>
    public static IEnumerable<SketchShape> Of(MapIntent intent, IReadOnlyDictionary<string, RoomGround>? rooms)
    {
        var expanded = PlayIds.Assign(SymmetryExpander.Expand(intent));
        string TeamColor(string team) => expanded.Teams?.FirstOrDefault(t => t.Id == team)?.Color ?? team;

        foreach (var spawn in expanded.Spawns)
            foreach (var shape in Room(spawn.Id, StructuralRoles.Spawn, TeamColor(spawn.Team), spawn.Protection,
                                       spawn.House, rooms))
                yield return shape;

        foreach (var wool in expanded.Wools ?? [])
            foreach (var shape in Room(wool.Id, StructuralRoles.WoolRoom,
                                       wool.Color.Length > 0 ? wool.Color : TeamColor(wool.Owner),
                                       wool.Protection, wool.House, rooms))
                yield return shape;
    }

    private static IEnumerable<SketchShape> Room(
        string id, string role, string color, List<Geom.Rect> protection, RoomHouse? house,
        IReadOnlyDictionary<string, RoomGround>? rooms)
    {
        // A region stated as a union has no one rectangle to draw, and the sketch draws rooms as rectangles.
        if (protection.Count != 1) yield break;
        yield return Shape(id, role, color, protection[0], rooms);
        // Each wing is drawn as the building it is; the first, which holds the room, keeps the bare id.
        var wing = 0;
        foreach (var rect in house?.Rects() ?? [])
            yield return Shape(++wing == 1 ? $"{id}-building" : $"{id}-building-{wing}",
                StructuralRoles.Building, color, rect, rooms);
    }

    private static SketchShape Shape(
        string id, string role, string color, Geom.Rect rect, IReadOnlyDictionary<string, RoomGround>? rooms)
    {
        var ground = rooms is not null && rooms.TryGetValue(id, out var stated) ? stated : null;
        return new SketchShape
        {
            Id = id, Type = "rectangle", Operation = "add", Role = role, Color = color,
            MinX = rect.MinX, MinZ = rect.MinZ, MaxX = rect.MaxX, MaxZ = rect.MaxZ,
            Floor = ground?.Floor, BaseHeight = ground?.BaseHeight, AnchorHeights = ground?.AnchorHeights,
            ReliefScope = ground?.ReliefScope, HeightAuthored = ground?.HeightAuthored, Doors = ground?.Doors,
            Theme = ground?.Theme, Material = ground?.Material,
        };
    }

    private static RoomGround Ground(SketchShape shape) => new()
    {
        Floor = shape.Floor, BaseHeight = shape.BaseHeight, AnchorHeights = shape.AnchorHeights,
        ReliefScope = shape.ReliefScope, HeightAuthored = shape.HeightAuthored, Doors = shape.Doors,
        Theme = shape.Theme, Material = shape.Material,
    };
}
