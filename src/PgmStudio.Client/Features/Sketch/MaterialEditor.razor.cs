using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;

namespace PgmStudio.Client.Features.Sketch;

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
    [Parameter] public string? Label { get; set; }
    /// <summary>Renders inside another material — indented and ruled, rather than as a top-level block.</summary>
    [Parameter] public bool Nested { get; set; }
    [Parameter] public EventCallback OnChanged { get; set; }
    /// <summary>Set only where the material is one entry of a list the author may shorten.</summary>
    [Parameter] public EventCallback OnRemove { get; set; }

    private string Kind => ThemeNode.KindOf(Node);
    private int Int(string field, int fallback) => ThemeNode.Int(Node, field, fallback);
    private Task Changed() => OnChanged.InvokeAsync();

    private JsonObject Neutral => ThemeNode.Child(Node, ThemeFields.Neutral, () => ThemeFields.Solid(159, 8));

    /// <summary>One entry of a material's child list, with everything the markup binds to. A pattern's entry
    /// is a bare material; a layer or a stripe wraps one with the extent it claims, which is what
    /// <see cref="Extent"/> and <see cref="SetExtent"/> reach — 0 where the list has no extent.</summary>
    private sealed record Entry(
        JsonNode Node, int Index, JsonObject Material, int Extent,
        EventCallback Remove, EventCallback<ChangeEventArgs> SetExtent);

    // The wrapped lists carry their extent under different names; the bare ones carry none.
    private static string? ExtentField(string field) => field switch
    {
        ThemeFields.Layers => ThemeFields.Thickness,
        ThemeFields.Runs => ThemeFields.Width,
        _ => null,
    };

    private IEnumerable<Entry> List(string field)
    {
        var array = ThemeNode.Array(Node, field);
        var extentField = ExtentField(field);
        for (var i = 0; i < array.Count; i++)
        {
            var node = array[i];
            if (node is null) continue;
            var wrapper = ThemeNode.AsObject(node);
            var material = extentField is null
                ? wrapper
                : ThemeNode.Child(wrapper, ThemeFields.Material, () => ThemeFields.Solid(1));
            var index = i;
            yield return new Entry(
                node, index, material,
                extentField is null ? 0 : ThemeNode.Int(wrapper, extentField, 1),
                EventCallback.Factory.Create(this, () => Remove(field, index)),
                EventCallback.Factory.Create<ChangeEventArgs>(this, e => SetExtent(wrapper, extentField, e)));
        }
    }

    // ── edits ──────────────────────────────────────────────────────────────────────────────────────
    // Every one rewrites the node and reports upward; the parent re-serializes the theme and re-previews.

    private async Task ChangeKind(ChangeEventArgs e)
    {
        var kind = (string?)e.Value;
        if (kind is null || kind == Kind) return;
        // A kind change replaces the material wholesale rather than keeping fields the new kind cannot read —
        // a leftover `cellSize` on a solid would be silently dropped and reappear on switching back.
        var replacement = ThemeFields.NewMaterial(kind);
        Node.Clear();
        foreach (var (name, value) in replacement.ToList())
        {
            replacement.Remove(name);
            Node[name] = value;
        }
        await Changed();
    }

    private Task PickSolid(PaintBlockDto block)
    {
        ThemeNode.Set(Node, ThemeFields.Id, block.Id);
        ThemeNode.Set(Node, ThemeFields.Data, block.Data);
        return Changed();
    }

    private Task PickTintBlock(PaintBlockDto block)
    {
        ThemeNode.Set(Node, ThemeFields.BlockId, block.Id);
        return Changed();
    }

    private Task SetSeed(ChangeEventArgs e) => SetScalar(ThemeFields.Seed, e, 0, 0);
    private Task SetCellSize(ChangeEventArgs e) => SetScalar(ThemeFields.CellSize, e, 8, 1);
    private Task SetScale(ChangeEventArgs e) => SetScalar(ThemeFields.Scale, e, 16, 1);
    private Task SetOctaves(ChangeEventArgs e) => SetScalar(ThemeFields.Octaves, e, 3, 1);

    private Task SetScalar(string field, ChangeEventArgs e, int fallback, int min)
    {
        ThemeNode.Set(Node, field, Math.Max(min, Parse(e, fallback)));
        return Changed();
    }

    private Task SetExtent(JsonObject wrapper, string? field, ChangeEventArgs e)
    {
        if (field is null) return Task.CompletedTask;
        ThemeNode.Set(wrapper, field, Math.Max(1, Parse(e, 1)));
        return Changed();
    }

    private static int Parse(ChangeEventArgs e, int fallback)
        => int.TryParse((string?)e.Value, out var value) ? value : fallback;

    private Task AddLayer() => Add(ThemeFields.Layers);
    private Task AddStripe() => Add(ThemeFields.Runs);
    private Task AddPaletteEntry() => Add(ThemeFields.Palette);
    private Task AddStop() => Add(ThemeFields.Stops);

    private Task Add(string field)
    {
        ThemeNode.Array(Node, field).Add(ThemeFields.NewEntry(field));
        return Changed();
    }

    private Task Remove(string field, int index)
    {
        var array = ThemeNode.Array(Node, field);
        // A pattern with nothing left resolves to bare stone, so the last entry stays put.
        if (index >= 0 && index < array.Count && array.Count > 1) array.RemoveAt(index);
        return Changed();
    }
}
