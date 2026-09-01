using System.Text.Json;
using PgmStudio.Contracts;
using PgmStudio.Data.Schema;
using PgmStudio.Data.Theme;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Services;

/// <summary>
/// The prop recipes a placement names: trees and boulders, row ⇄ recipe ⇄ request.
///
/// <para><see cref="HousePartLibrary"/>'s sibling. The clamps live here rather than on the record, so a row
/// hand-edited into the database still draws something — the recipe's own <c>Reach</c> and <c>Shape</c> hold
/// their ranges again downstream, and holding twice is cheaper than one caller forgetting.</para>
///
/// <para>A recipe's card is drawn through the pass that builds it (<see cref="DressingPreview"/>), so a library
/// is browsed by what its entries look like rather than by their numbers — which for a tree is the whole point,
/// since six woods differ in colour and six species differ in shape.</para>
/// </summary>
public sealed class PropStyleLibrary(PropStyleStore store)
{
    /// <summary>The theme a card is grown on. A recipe has no map behind it, so the sample ground is the one
    /// every other card in the library stands on.</summary>
    private static TerrainTheme Sample => ThemePresets.Meadow;

    // ── trees ─────────────────────────────────────────────────────────────────────────────────────────
    public async Task<IReadOnlyList<(TreeStyleRow Row, string Card)>> ListTreesAsync(CancellationToken ct = default)
        => [.. (await store.ListTreesAsync(ct)).Select(row => (row, Card(TreeProp(row))))];

    public static TreeStyle TreeOf(TreeStyleRow row) => new()
    {
        Form = TreeForms.Canonical(row.Form) == TreeForms.Grown ? TreeForm.Grown : TreeForm.Template,
        Species = TreeSpeciesNames.Canonical(row.Species),
        Wood = TreeWoodNames.Canonical(row.Wood),
        Height = row.Height,
        Stems = row.Stems,
        Leader = row.Leader,
        Flow = row.Flow,
        BranchAngle = row.BranchAngle,
        Levels = row.Levels,
        Whorled = row.Whorled,
        LeafSize = row.LeafSize,
    };

    public static TreeStyleRow RowOf(TreeStyleSaveRequest req) => new()
    {
        Name = req.Name,
        Form = TreeForms.Canonical(req.Form),
        Species = TreeSpeciesNames.Canonical(req.Species),
        Wood = TreeWoodNames.Canonical(req.Wood),
        Height = Math.Clamp(req.Height, 5, 40),
        Stems = Math.Clamp(req.Stems, 1, 3),
        Leader = Math.Clamp(req.Leader, 0, 1),
        Flow = Math.Clamp(req.Flow, 0, 1),
        BranchAngle = Math.Clamp(req.BranchAngle, 0.2, 1.5),
        Levels = Math.Clamp(req.Levels, 2, 3),
        Whorled = req.Whorled,
        LeafSize = Math.Clamp(req.LeafSize, 0.2, 1),
    };

    public static TreeStyleDetail ToDetail(TreeStyleRow row) => new(
        row.Id, row.Name, TreeForms.Canonical(row.Form), row.Species, row.Wood, row.Height,
        row.Stems, row.Leader, row.Flow, row.BranchAngle, row.Levels, row.Whorled, row.LeafSize);

    /// <summary>The draft as the editor's own stage draws it — the same recipe as a browse card, at the
    /// size a knob is judged at rather than the size a row is scanned at.</summary>
    public static string CardOf(TreeStyleSaveRequest draft) => Card(TreeProp(RowOf(draft)), StageCell);

    private static TreeProp TreeProp(TreeStyleRow row)
        => new() { Id = "sample", X = 0, Z = 0, Seed = 7, Style = TreeOf(row) };

    // ── boulders ──────────────────────────────────────────────────────────────────────────────────────
    public async Task<IReadOnlyList<(BoulderStyleRow Row, string Card)>> ListBouldersAsync(
        CancellationToken ct = default)
        => [.. (await store.ListBouldersAsync(ct)).Select(row => (row, Card(BoulderProp(row))))];

    public static BoulderStyle BoulderOf(BoulderStyleRow row) => new()
    {
        Form = BoulderForms.Canonical(row.Form) switch
        {
            BoulderForms.Angular => BoulderForm.Angular,
            BoulderForms.Outcrop => BoulderForm.Outcrop,
            BoulderForms.Cairn => BoulderForm.Cairn,
            _ => BoulderForm.Round,
        },
        Size = row.Size,
        Mossy = row.Mossy,
        Rock = Material(row.Rock),
    };

    public static BoulderStyleRow RowOf(BoulderStyleSaveRequest req) => new()
    {
        Name = req.Name,
        Form = BoulderForms.Canonical(req.Form),
        Size = Math.Clamp(req.Size, 2, 10),
        Mossy = req.Mossy,
        // Kept as the material's own JSON rather than re-serialized from a parse: a rock states any of the
        // fourteen kinds, and round-tripping one through a narrower type is how a kind goes missing.
        Rock = Readable(req.Rock),
    };

    public static BoulderStyleDetail ToDetail(BoulderStyleRow row) => new(
        row.Id, row.Name, BoulderForms.Canonical(row.Form), row.Size, row.Mossy, row.Rock);

    public static string CardOf(BoulderStyleSaveRequest draft) => Card(BoulderProp(RowOf(draft)), StageCell);

    private static BoulderProp BoulderProp(BoulderStyleRow row)
        => new() { Id = "sample", X = 0, Z = 0, Seed = 7, Style = BoulderOf(row) };

    /// <summary>A stated material, or plain stone where the JSON is unreadable — a recipe that cannot say what
    /// its rock is still draws a rock.</summary>
    private static TerrainMaterial Material(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<TerrainMaterial>(json, TerrainThemeJson.Options)
                   ?? new SolidMaterial(Minecraft.Palette.Blocks.Stone);
        }
        catch (JsonException) { return new SolidMaterial(Minecraft.Palette.Blocks.Stone); }
    }

    private static string Readable(string json)
    {
        try
        {
            using var read = JsonDocument.Parse(json);
            return json;
        }
        catch (JsonException) { return """{"kind":"solid","id":1,"data":0}"""; }
    }

    /// <summary>How many pixels a block takes on the editor's stage, against the browse row's 3. A recipe is
    /// tuned by watching one knob move the picture, which wants the picture bigger than a row of them does.</summary>
    private const int StageCell = 9;

    /// <summary>One recipe's card: the section, drawn through the pass that builds it.</summary>
    private static string Card(PlacedProp prop, int cell = 3)
        => DressingPreview.Views(prop, Sample, cell).Section;
}
