using System.Text.Json;
using PgmStudio.Contracts;
using PgmStudio.Data.Schema;
using PgmStudio.Data.Theme;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Minecraft.Library;
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
/// since seven woods differ in colour and seven species differ in shape.</para>
/// </summary>
public sealed class PropStyleLibrary(PropStyleStore store)
{
    /// <summary>The theme a card is grown on. A recipe has no map behind it, so the sample ground is the one
    /// every other card in the library stands on.</summary>
    private static TerrainTheme Sample => SeedFolder.Meadow;

    // ── trees ─────────────────────────────────────────────────────────────────────────────────────────
    public async Task<IReadOnlyList<(TreeStyleRow Row, string Card)>> ListTreesAsync(CancellationToken ct = default)
        => [.. (await store.ListTreesAsync(ct)).Select(row => (row, Card(TreeProp(row), TreeOf(row))))];

    public static TreeStyle TreeOf(TreeStyleRow row) => new()
    {
        Form = TreeForms.Canonical(row.Form) == TreeForms.Copied ? TreeForm.Copied : TreeForm.Template,
        Species = TreeSpeciesNames.Canonical(row.Species),
        Height = row.Height,
        Body = BodyOf(row.Body),
        Builder = TreeForms.Canonical(row.Form) == TreeForms.Copied && row.CutBuilder is { Length: > 0 } builder
            ? builder : null,
    };

    /// <summary>The tree gate a save passes: a <c>copied</c> recipe states where it was cut
    /// (<see cref="DressingRules.UncutCopy"/>). A template is never refused here.</summary>
    public static Findings Check(TreeStyleSaveRequest req) =>
        TreeForms.Canonical(req.Form) != TreeForms.Copied || req.Cut is { World.Length: > 0 }
            ? Findings.None
            : Findings.Of(new Finding(DressingRules.UncutCopy,
                $"'{req.Name}' is filed as a copied tree and states no cut — a copied tree is one cut out of a "
                + "world, and the cut (world, foot, time) is what the cutter records",
                Field: "cut"));

    /// <summary>A copied recipe's height is what its body stands, read rather than stated: a knob that
    /// disagrees with the blocks would size a preview's sample patch for a tree that is not there. The cut is
    /// kept on a copy and dropped from a template, the same way the body is.</summary>
    public static TreeStyleRow RowOf(TreeStyleSaveRequest req)
    {
        var copied = TreeForms.Canonical(req.Form) == TreeForms.Copied;
        var cut = copied ? req.Cut : null;
        return new()
        {
            Name = req.Name,
            Form = TreeForms.Canonical(req.Form),
            Species = TreeSpeciesNames.Canonical(req.Species),
            Height = copied
                ? new TreeStyle { Form = TreeForm.Copied, Body = req.Body }.BodyHeight
                : Math.Clamp(req.Height, 5, 40),
            Body = copied && req.Body is { Length: > 0 } ? JsonSerializer.Serialize(req.Body) : "",
            CutWorld = cut?.World,
            CutX = cut?.X,
            CutY = cut?.Y,
            CutZ = cut?.Z,
            CutAt = cut?.At,
            CutBuilder = cut?.Builder is { } builder && builder.Trim() is { Length: > 0 } named ? named : null,
        };
    }

    /// <summary>A tree cut out of a world as the row the library files it under: the recipe, and the cut — the
    /// world and the foot it stood on. The time of the cut is the caller's, since the cut it is read from does
    /// not record one.</summary>
    public static TreeStyleRow RowOf(string name, string world, (int X, int Y, int Z) foot, TreeStyle tree) => new()
    {
        Name = name,
        Form = TreeForms.Copied,
        Species = TreeSpeciesNames.Canonical(tree.Species),
        Height = tree.BodyHeight,
        Body = tree.Body is { Count: > 0 } body ? JsonSerializer.Serialize(body) : "",
        CutWorld = world,
        CutX = foot.X,
        CutY = foot.Y,
        CutZ = foot.Z,
        CutBuilder = tree.Builder is { } builder && builder.Trim() is { Length: > 0 } named ? named : null,
    };

    public static TreeStyleDetail ToDetail(TreeStyleRow row) => new(
        row.Id, row.Name, TreeForms.Canonical(row.Form), row.Species, row.Height,
        BodyOf(row.Body)?.Select(cell => cell).ToArray(), CutOf(row), row.SeedKey is not null);

    /// <summary>The cut a row records, or none where it records no world.</summary>
    private static TreeCut? CutOf(TreeStyleRow row) =>
        row is { CutWorld.Length: > 0, CutX: { } x, CutY: { } y, CutZ: { } z, CutAt: { } at }
            ? new TreeCut(row.CutWorld, x, y, z, DateTime.SpecifyKind(at, DateTimeKind.Utc), row.CutBuilder)
            : null;

    /// <summary>The body a row stores, or nothing where it stores none or stores something that is not a
    /// list of rows — a hand-edited column still answers a recipe rather than a 500.</summary>
    private static int[][]? BodyOf(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<int[][]>(json); }
        catch (JsonException) { return null; }
    }

    /// <summary>The draft as the editor's own stage draws it — the same recipe as a browse card, at the
    /// size a knob is judged at rather than the size a row is scanned at.</summary>
    public static string CardOf(TreeStyleSaveRequest draft) => Card(TreeProp(RowOf(draft)), TreeOf(RowOf(draft)), StageCell);

    private static TreeProp TreeProp(TreeStyleRow row)
        => new() { Id = "sample", X = 0, Z = 0, Seed = 7, Style = TreeOf(row) };

    // ── boulders ──────────────────────────────────────────────────────────────────────────────────────
    public async Task<IReadOnlyList<(BoulderStyleRow Row, string Card)>> ListBouldersAsync(
        CancellationToken ct = default)
        => [.. (await store.ListBouldersAsync(ct)).Select(row => (row, Card(BoulderProp(row), BoulderOf(row))))];

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

    /// <summary>A boulder recipe as the row the library files it under.</summary>
    public static BoulderStyleRow RowOf(string name, BoulderStyle boulder) => new()
    {
        Name = name,
        Form = boulder.Form switch
        {
            BoulderForm.Angular => BoulderForms.Angular,
            BoulderForm.Outcrop => BoulderForms.Outcrop,
            BoulderForm.Cairn => BoulderForms.Cairn,
            _ => BoulderForms.Round,
        },
        Size = boulder.Size,
        Mossy = boulder.Mossy,
        Rock = TerrainThemeJson.Serialize(boulder.Rock),
    };

    public static BoulderStyleDetail ToDetail(BoulderStyleRow row) => new(
        row.Id, row.Name, BoulderForms.Canonical(row.Form), row.Size, row.Mossy, row.Rock, row.SeedKey is not null);

    public static string CardOf(BoulderStyleSaveRequest draft) => Card(BoulderProp(RowOf(draft)), BoulderOf(RowOf(draft)), StageCell);

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
    private static string Card(PlacedProp prop, PropStyle recipe, int cell = 3)
        => Drawings.Svg($"prop-card/{cell}",
            DressingJson.SerializeProp(prop) + "\n" + DressingJson.SerializeStyle(recipe),
            () => DressingPreview.Views(prop, Sample, cell).Section);
}
