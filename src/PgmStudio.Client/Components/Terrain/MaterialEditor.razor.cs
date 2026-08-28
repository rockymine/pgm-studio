using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;
using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Components;

/// <summary>
/// The form behind one terrain-paint material (docs/world-export/terrain-painting.md §3). It reads and writes
/// the material's own JSON node — the wire format, not a copy — so switching a bucket from a solid to a
/// voronoi rewrites that node in place and the theme is whatever the nodes say. Recursive: a pattern's entries,
/// a stack's layers and a tint's neutral fallback each render another of these.
/// </summary>
public partial class MaterialEditor
{
    /// <summary>The material node this edits, in place.</summary>
    [Parameter, EditorRequired] public JsonObject Node { get; set; } = [];
    [Parameter] public IReadOnlyList<PaintBlockDto> Blocks { get; set; } = [];

    /// <summary>The saved styles a slot may be filled from. Empty offers nothing, which is what a surface with
    /// no library behind it wants.</summary>
    [Parameter] public IReadOnlyList<StyleDto> Styles { get; set; } = [];
    [Parameter] public string? Label { get; set; }
    /// <summary>Renders inside another material — indented and ruled, rather than as a top-level block.</summary>
    [Parameter] public bool Nested { get; set; }
    /// <summary>Draws one level only: a nested material renders as its own row and nothing below it, because
    /// the outline beside the form is carrying the nest. The flag passes down, so setting it at the root
    /// flattens the whole form.</summary>
    [Parameter] public bool Flat { get; set; }
    /// <summary>Extra controls for the material's own row, between the kind and the remove button. Set by a
    /// list that gives its entries an extent (a layer's courses, a band's depth, a stripe's width): the
    /// number belongs to the entry, not to the material, but it reads as part of the same row.</summary>
    [Parameter] public RenderFragment? HeadExtra { get; set; }
    [Parameter] public EventCallback OnChanged { get; set; }
    /// <summary>Set only where the material is one entry of a list the author may shorten.</summary>
    [Parameter] public EventCallback OnRemove { get; set; }

    /// <summary>Whether the "?" beside the kind is showing its note. Top-level only — a nested material
    /// never renders the mark, so it never carries the state either.</summary>
    private bool helpOpen;

    /// <summary>How many fills this slot has taken. It keys the style select, which is what makes the control
    /// go back to reading "fill from a style…" after one: the select carries a placeholder value the diff
    /// sees as unchanged, so nothing would push the browser's own selection back off the name it landed on,
    /// and the row would go on claiming a provenance it does not store.</summary>
    private int filled;

    /// <summary>Whether this instance is one of a flattened form's rows rather than a form of its own.</summary>
    private bool Stub => Nested && Flat;

    private string Kind => JsonEdit.KindOf(Node);
    private int Int(string field, int fallback) => JsonEdit.Int(Node, field, fallback);
    private Task Changed() => OnChanged.InvokeAsync();

    private JsonObject Neutral => JsonEdit.Child(Node, ThemeFields.Neutral, () => ThemeFields.Solid(159, 8));

    /// <summary>A checkerboard's two squares. They default apart rather than both to stone, since a board of
    /// one material is not a board and an author would have to change one before seeing anything.</summary>
    private JsonObject Even => JsonEdit.Child(Node, ThemeFields.Even, () => ThemeFields.Solid(1, 0));
    private JsonObject Odd => JsonEdit.Child(Node, ThemeFields.Odd, () => ThemeFields.Solid(159, 15));

    /// <summary>A wall frame's ink and the panel it encloses. "Panel" rather than "fill" in the editor because
    /// fill is already the name of a whole terrain bucket, and a material inside one is not that.</summary>
    private JsonObject Edge => JsonEdit.Child(Node, ThemeFields.Edge, () => ThemeFields.Solid(159, 15));
    private JsonObject Panel => JsonEdit.Child(Node, ThemeFields.Fill, () => ThemeFields.Solid(155, 0));

    /// <summary>What the three field patterns are, in one sentence each — the rest of their blurb is shared,
    /// because everything except the bend is.</summary>
    private string FieldBlurb => Kind switch
    {
        MaterialKind.Turbulence => "A field folded at every crossing, so it creases: billowed, marbled bands.",
        MaterialKind.Electric => "A field whose crossings are thin branching filaments, everything else falling away from them.",
        _ => "A smooth fractal field — cloudy regions fading into one another.",
    };

    /// <summary>What the rise does, shared by every area pattern because it means the same thing in all of
    /// them: at 0 the field is of the plane, so a column resolves to one block all the way down and the pattern
    /// only ever decides the ground.</summary>
    private const string RiseNote =
        "A rise of 0 paints the ground and leaves every wall face striped; give it a vertical period in blocks "
        + "and the pattern carries through the depth of the terrain instead.";

    /// <summary>What one of a kind's single children is called.</summary>
    private string ChildLabel(string field)
        => MaterialTree.ChildrenOf(Kind).FirstOrDefault(child => child.Field == field).Label ?? field;

    /// <summary>What one entry of a list is called, by where it sits.</summary>
    private string EntryLabel(string field, int index)
        => MaterialTree.EntryLabel(field, index, JsonEdit.Array(Node, field).Count);

    /// <summary>One entry of a material's child list, with everything the markup binds to. A pattern's entry
    /// is a bare material; a layer or a stripe wraps one with the extent it claims, which is what
    /// <see cref="Extent"/> and <see cref="SetExtent"/> reach — 0 where the list has no extent.</summary>
    private sealed record Entry(
        JsonNode Node, int Index, JsonObject Material, int Extent,
        EventCallback Remove, EventCallback<ChangeEventArgs> SetExtent);

    private IEnumerable<Entry> List(string field)
    {
        var array = JsonEdit.Array(Node, field);
        var extentField = MaterialTree.ExtentOf(field);
        for (var i = 0; i < array.Count; i++)
        {
            var node = array[i];
            if (node is null) continue;
            var wrapper = JsonEdit.AsObject(node);
            var material = extentField is null
                ? wrapper
                : JsonEdit.Child(wrapper, ThemeFields.Material, () => ThemeFields.Solid(1));
            var index = i;
            yield return new Entry(
                node, index, material,
                extentField is null ? 0 : JsonEdit.Int(wrapper, extentField, 1),
                EventCallback.Factory.Create(this, () => Remove(field, index)),
                EventCallback.Factory.Create<ChangeEventArgs>(this, e => SetExtent(wrapper, extentField, e)));
        }
    }

    // ── edits ──────────────────────────────────────────────────────────────────────────────────────
    // Every one rewrites the node and reports upward; the parent re-serializes the theme and re-previews.

    private async Task ChangeKind(string kind)
    {
        if (kind.Length == 0 || kind == Kind) return;
        JsonEdit.Replace(Node, ThemeFields.NewMaterial(kind));
        await Changed();
    }

    /// <summary>The saved styles as rows, grouped by kind — the same grouping every list of them uses, because
    /// what tells two styles apart at a glance is what kind of material each is.</summary>
    private IReadOnlyList<SelectOption> StyleRows =>
    [
        .. Styles.Select(style => new SelectOption(
            style.Id.ToString(), style.Name, Group: MaterialKind.NameOf(style.Kind))),
    ];

    /// <summary>
    /// Fill this slot with a saved style. A style <b>is</b> a material and materials nest, so a style inside a
    /// layer, a band or a patch is nesting rather than a new kind of thing — there is nothing for the document
    /// to say that it cannot already say.
    ///
    /// <para>What lands is a <b>copy</b>. The material tree is the wire format the painter deserializes and the
    /// form a map snapshots, so a slot holding a style's name would have to be resolved by everything that
    /// reads one, and a map's paint would change under it whenever the library did. Once filled it is ordinary
    /// material JSON and is edited like any other: the style is where it came from, not what it is.</para>
    /// </summary>
    private async Task FillFromStyle(string value)
    {
        if (!long.TryParse(value, out var id)) return;
        if (Styles.FirstOrDefault(style => style.Id == id) is not { } style) return;
        if (JsonNode.Parse(style.Params) is not JsonObject material) return;
        JsonEdit.Replace(Node, material);
        filled++;
        await Changed();
    }

    private Task PickSolid(PaintBlockDto block)
    {
        JsonEdit.Set(Node, ThemeFields.Id, block.Id);
        JsonEdit.Set(Node, ThemeFields.Data, block.Data);
        return Changed();
    }

    private Task PickTintBlock(PaintBlockDto block)
    {
        JsonEdit.Set(Node, ThemeFields.BlockId, block.Id);
        return Changed();
    }

    private Task SetSeed(ChangeEventArgs e) => SetScalar(ThemeFields.Seed, e, 0, 0);
    private Task SetCellSize(ChangeEventArgs e) => SetScalar(ThemeFields.CellSize, e, 10, 1);
    private Task SetWarp(ChangeEventArgs e) => SetScalar(ThemeFields.Warp, e, 4, 0);
    private Task SetScale(ChangeEventArgs e) => SetScalar(ThemeFields.Scale, e, 16, 1);
    private Task SetRise(ChangeEventArgs e) => SetScalar(ThemeFields.Rise, e, 0, 0);
    private Task SetSize(ChangeEventArgs e) => SetScalar(ThemeFields.Size, e, 1, 1);
    private Task SetAngle(ChangeEventArgs e) => SetScalar(ThemeFields.Angle, e, 45, 1);
    private Task SetThickness(ChangeEventArgs e) => SetScalar(ThemeFields.Thickness, e, 1, 1);

    /// <summary>Slope is the one scalar with no floor: a negative one leans the stripes the other way.</summary>
    private Task SetSlope(ChangeEventArgs e)
    {
        JsonEdit.Set(Node, ThemeFields.Slope, Parse(e, 1));
        return Changed();
    }

    /// <summary>Jitter is a percentage, so it clamps at both ends rather than only below.</summary>
    private Task SetJitter(ChangeEventArgs e)
    {
        JsonEdit.Set(Node, ThemeFields.Jitter, Math.Clamp(Parse(e, 50), 0, 100));
        return Changed();
    }
    private Task SetOctaves(ChangeEventArgs e) => SetScalar(ThemeFields.Octaves, e, 3, 1);

    private Task SetScalar(string field, ChangeEventArgs e, int fallback, int min)
    {
        JsonEdit.Set(Node, field, Math.Max(min, Parse(e, fallback)));
        return Changed();
    }

    private Task SetExtent(JsonObject wrapper, string? field, ChangeEventArgs e)
    {
        if (field is null) return Task.CompletedTask;
        JsonEdit.Set(wrapper, field, Math.Max(1, Parse(e, 1)));
        return Changed();
    }

    private static int Parse(ChangeEventArgs e, int fallback)
        => int.TryParse((string?)e.Value, out var value) ? value : fallback;

    private Task Add(string field)
    {
        JsonEdit.Array(Node, field).Add(ThemeFields.NewEntry(field));
        return Changed();
    }

    /// <summary>The families the offered blocks carry, whole and in offer order — what "fill from a family"
    /// chooses between. Derived from the block list rather than fetched beside it: a block already names the
    /// group it belongs to and whether that group is a family, so grouping the flagged ones recovers the
    /// families exactly, including the stained shades a family claims.</summary>
    private IEnumerable<IGrouping<string, PaintBlockDto>> Families =>
        Blocks.Where(block => block.InFamily).GroupBy(block => block.Group);

    /// <summary>The families on offer, each over how many blocks it lays.</summary>
    private IReadOnlyList<SelectOption> FamilyRows =>
        [.. Families.Select(family => new SelectOption(family.Key, $"{family.Key} ({family.Count()})"))];

    /// <summary>The family a list currently holds, whole and in order, or empty for one that holds anything
    /// else. Read from the entries rather than remembered, so it stays true through a hand edit: it names the
    /// family the moment a list is filled from one, and falls back to the offer the moment an author narrows
    /// it — which is what narrowing a family is for.</summary>
    private string MatchedFamily(string field)
    {
        var chosen = List(field)
            .Select(entry => JsonEdit.KindOf(entry.Material) == MaterialKind.Solid
                ? (JsonEdit.Int(entry.Material, ThemeFields.Id, -1), JsonEdit.Int(entry.Material, ThemeFields.Data, 0))
                : (-1, -1))
            .ToList();
        foreach (var family in Families)
        {
            var blocks = family.Select(block => (block.Id, block.Data)).ToList();
            if (blocks.Count == chosen.Count && blocks.SequenceEqual(chosen)) return family.Key;
        }
        return string.Empty;
    }

    /// <summary>Replaces a list with one entry per block of a family — the whole ground an author reaches for,
    /// laid into the pattern in the family's own light-to-dark order. It replaces rather than appends because a
    /// family <em>is</em> the palette; entries are then removed one by one to narrow it, which is a shorter road
    /// than adding five and picking each block by hand.</summary>
    private Task ApplyFamily(string field, string name)
    {
        var family = Families.FirstOrDefault(group => group.Key == name);
        if (family is null) return Task.CompletedTask;
        var array = JsonEdit.Array(Node, field);
        array.Clear();
        foreach (var block in family)
            array.Add(ThemeFields.Entry(field, ThemeFields.Solid(block.Id, block.Data)));
        return Changed();
    }

    private Task Remove(string field, int index)
    {
        var array = JsonEdit.Array(Node, field);
        // A pattern with nothing left resolves to bare stone, so the last entry stays put.
        if (index >= 0 && index < array.Count && array.Count > 1) array.RemoveAt(index);
        return Changed();
    }
}
