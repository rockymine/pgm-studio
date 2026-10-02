using PgmStudio.Contracts;
using PgmStudio.Data.Schema;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Services;

/// <summary>
/// What fills a slot — a theme's bucket, a building's course: one block written in place, or a library pattern
/// bound by id. A single block is not a pattern, so it never takes a row of its own; only a material that mixes
/// blocks, or tints one by team, is a pattern.
/// </summary>
public static class Slots
{
    /// <summary>The block stored in a slot's columns, or null where the slot holds none.</summary>
    public static SlotBlockDto? BlockOf(int? id, int data, bool laid) => id is { } block ? new(block, data, laid) : null;

    /// <summary>The block a material is, when it is one: a solid block, or a log laid along its run. Null for
    /// everything else, which is a pattern.</summary>
    public static SlotBlockDto? AsBlock(TerrainMaterial material) => material switch
    {
        SolidMaterial solid => new SlotBlockDto(solid.Id, solid.Data, Laid: false),
        LaidLogMaterial laid => new SlotBlockDto(laid.Id, laid.Data, Laid: true),
        _ => null,
    };

    /// <summary>Whether a stored kind is one block rather than a pattern.</summary>
    public static bool IsBlockKind(string kind) => kind is MaterialKind.Solid or MaterialKind.LaidLog;

    /// <summary>The material one block is laid as.</summary>
    public static TerrainMaterial MaterialOf(SlotBlockDto block)
        => block.Laid ? new LaidLogMaterial(block.Id, block.Data) : new SolidMaterial(block.Id, block.Data);

    /// <summary>A slot's material as the stored kind and JSON a theme's binding carries.</summary>
    public static (string Kind, string Json) Serialized(SlotBlockDto block)
        => (block.Laid ? MaterialKind.LaidLog : MaterialKind.Solid, TerrainThemeJson.Serialize(MaterialOf(block)));

    /// <summary>The material a slot resolves to: its block, else the pattern it binds, else null — where it
    /// names neither, or a pattern the library no longer holds or cannot read. Forgiving for the reason a style's
    /// card picture is: <c>params_json</c> is a hand-editable leaf, and a building that draws without one bad
    /// course is more use than a library that refuses to list.</summary>
    public static TerrainMaterial? Resolve(SlotBlockDto? block, long? styleId, IReadOnlyDictionary<long, StyleRow> styles)
    {
        if (block is not null) return MaterialOf(block);
        if (styleId is not { } id || !styles.TryGetValue(id, out var style)) return null;
        try { return TerrainThemeJson.DeserializeMaterial(style.Params); }
        catch { return null; }
    }
}
