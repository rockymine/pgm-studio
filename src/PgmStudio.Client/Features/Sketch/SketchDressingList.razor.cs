using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>
/// The Dressing phase's sidebar: every prop on the map, under one collapsible heading per kind with its count,
/// in the order each was placed. A row says what the prop is — a tree by its species and height, a building by
/// its shell — and where, and rings its prop on the board while the pointer is on it. The canvas is where a prop is normally reached, but a marker in a dense corner is a
/// small target and a list is the only view that says how much has been placed at all.
/// </summary>
public partial class SketchDressingList
{
    [Parameter] public IJSObjectReference? Handle { get; set; }
    /// <summary>The dressing state the bridge pushes (<c>OnDressing</c>).</summary>
    [Parameter] public string? StateJson { get; set; }
    [Inject] public IJSRuntime JS { get; set; } = default!;

    private sealed record Row(string Id, string Label, string Where, string WhereTitle);
    private sealed record Group(string Kind, string Icon, string Title, List<Row> Rows);

    private List<Group> groups = [];
    private int total;
    private string? selectedId;
    /// <summary>The kinds the author folded. A heading opens again when a prop under it is picked, so a prop
    /// picked on the canvas always has its row on show.</summary>
    private readonly HashSet<string> collapsed = [];
    private string? scrolledTo;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await JS.InvokeVoidAsync("studio.icons");
        if (selectedId is not null && selectedId != scrolledTo)
            await JS.InvokeVoidAsync("studio.scrollIntoView", RowElementId(selectedId));
        scrolledTo = selectedId;
    }

    protected override void OnParametersSet()
    {
        var previous = selectedId;
        groups = [];
        total = 0;
        selectedId = null;
        if (string.IsNullOrWhiteSpace(StateJson)) return;

        try
        {
            using var doc = JsonDocument.Parse(StateJson);
            var root = doc.RootElement;
            if (root.TryGetProperty("selectedId", out var selected) && selected.ValueKind == JsonValueKind.String)
                selectedId = selected.GetString();
            if (!root.TryGetProperty("props", out var props) || props.ValueKind != JsonValueKind.Array) return;
            var styles = root.TryGetProperty("styles", out var registry) && registry.ValueKind == JsonValueKind.Object
                ? registry : default;

            var byKind = new Dictionary<string, Group>();
            foreach (var prop in props.EnumerateArray())
            {
                var kind = Field(prop, "kind", "");
                if (!byKind.TryGetValue(kind, out var group))
                {
                    var names = PropKindNames.Of(kind);
                    byKind[kind] = group = new Group(kind, names.Icon, names.Many, []);
                }
                var row = RowOf(prop, kind, styles);
                group.Rows.Add(row);
                if (row.Id == selectedId && selectedId != previous) collapsed.Remove(kind);
                total++;
            }
            groups = [.. byKind.Values.OrderBy(group => IndexOf(group.Kind))];
        }
        catch (JsonException) { /* a state push the client cannot read lists nothing rather than throwing */ }
    }

    private static int IndexOf(string kind)
    {
        for (var i = 0; i < PropKindNames.All.Count; i++) if (PropKindNames.All[i].Kind == kind) return i;
        return PropKindNames.All.Count;
    }

    // A marker reads by its cell, an area by how far it runs, since those are the two questions a list of
    // placed things is asked.
    private static Row RowOf(JsonElement prop, string kind, JsonElement styles)
    {
        var id = Field(prop, "id", "");
        var recipe = Recipe(prop, styles);
        return kind switch
        {
            PropKinds.Tree => new Row(id, TreeLabel(prop, recipe), Cell(prop), "x, z"),
            PropKinds.Boulder => new Row(id, BoulderLabel(recipe), Cell(prop), "x, z"),
            PropKinds.Stroke => new Row(id, StrokeLabel(prop), Length(prop), "Length along the line"),
            PropKinds.Fluid => new Row(id, $"{Capital(Field(prop, PropFields.Form, PropFields.CanalForm))} channel", Length(prop), "Length along the line"),
            PropKinds.Flora => new Row(id, "Ground cover", Extent(prop), "Width × depth"),
            PropKinds.House => new Row(id, HouseLabel(prop), Footprint(prop), "Footprint, width × depth"),
            PropKinds.Chest => new Row(id, "Chest", Cell(prop), "x, z"),
            _ => new Row(id, Capital(kind), "", ""),
        };
    }

    /// <summary>The registry entry a tree's, a boulder's or a building's <c>style</c> key names, or nothing
    /// where none is named or the registry does not hold it.</summary>
    private static JsonElement Recipe(JsonElement prop, JsonElement styles)
        => styles.ValueKind == JsonValueKind.Object && Field(prop, PropFields.Style, "") is { Length: > 0 } key
           && styles.TryGetProperty(key, out var recipe) && recipe.ValueKind == JsonValueKind.Object ? recipe : default;

    /// <summary>A generated tree by its species and height; a hand-built one by the library name it was pulled
    /// under, since it has no species, and the courses it stands.</summary>
    private static string TreeLabel(JsonElement prop, JsonElement recipe)
    {
        if (recipe.ValueKind != JsonValueKind.Object) return "Oak · 12";
        if (Field(recipe, TreeFields.Form, TreeForms.Template) == TreeForms.Copied)
        {
            var name = Words(Field(prop, PropFields.Style, "tree"));
            return BodyHeight(recipe) is > 0 and var courses ? $"{name} · {courses}" : name;
        }
        return $"{Capital(Field(recipe, TreeFields.Species, PropFields.OakSpecies))} · {Whole(Number(recipe, TreeFields.Height, 12))}";
    }

    private static string BoulderLabel(JsonElement recipe)
    {
        if (recipe.ValueKind != JsonValueKind.Object) return "Round boulder · 4";
        var form = Capital(Field(recipe, BoulderFields.Form, PropFields.RoundForm));
        return $"{form} boulder · {Whole(Number(recipe, BoulderFields.Size, 4))}";
    }

    private static string StrokeLabel(JsonElement prop)
    {
        var style = Capital(Field(prop, PropFields.Style, PropFields.SolidStyle));
        var what = Bool(prop, PropFields.ClaimsGround) ? "path" : "paint";
        return $"{style} {what} · {Whole(2 * Number(prop, PropFields.Radius, 3))} wide";
    }

    /// <summary>A building by the shell its style key names, which is the library row it was picked from.</summary>
    private static string HouseLabel(JsonElement prop)
        => Field(prop, PropFields.Style, "") is { Length: > 0 } key ? Words(key) : "Building";

    private static int BodyHeight(JsonElement recipe)
    {
        if (!recipe.TryGetProperty("body", out var body) || body.ValueKind != JsonValueKind.Array) return 0;
        int low = int.MaxValue, high = int.MinValue;
        foreach (var cell in body.EnumerateArray())
        {
            if (cell.ValueKind != JsonValueKind.Array || cell.GetArrayLength() < 5) continue;
            var y = cell[1].GetInt32();
            low = Math.Min(low, y); high = Math.Max(high, y);
        }
        return low > high ? 0 : high - low + 1;
    }

    private static string Cell(JsonElement prop)
        => prop.TryGetProperty("x", out var x) && prop.TryGetProperty("z", out var z)
            ? $"{x.GetRawText()}, {z.GetRawText()}" : "";

    /// <summary>How far a drawn line runs, in blocks, along the points as drawn.</summary>
    private static string Length(JsonElement prop)
    {
        var points = Points(prop);
        if (points.Count < 2) return "";
        var length = 0.0;
        for (var i = 1; i < points.Count; i++)
            length += Math.Sqrt(Math.Pow(points[i].X - points[i - 1].X, 2) + Math.Pow(points[i].Z - points[i - 1].Z, 2));
        return $"{Whole(length)} long";
    }

    /// <summary>The box an area's outline spans, in whole blocks.</summary>
    private static string Extent(JsonElement prop)
    {
        var points = Points(prop);
        if (points.Count == 0) return "";
        var width = points.Max(point => point.X) - points.Min(point => point.X);
        var depth = points.Max(point => point.Z) - points.Min(point => point.Z);
        return $"{Whole(width)} × {Whole(depth)}";
    }

    private static List<(double X, double Z)> Points(JsonElement prop)
    {
        var points = new List<(double X, double Z)>();
        if (!prop.TryGetProperty("points", out var array) || array.ValueKind != JsonValueKind.Array) return points;
        foreach (var point in array.EnumerateArray())
            if (point.ValueKind == JsonValueKind.Array && point.GetArrayLength() >= 2)
                points.Add((point[0].GetDouble(), point[1].GetDouble()));
        return points;
    }

    /// <summary>A building's footprint in whole blocks, over the box drawn round every wing it carries. Each
    /// wing is stored as its two opposite <c>corners</c> rather than as a traced outline, so the box round them
    /// is what a one-line summary can afford; an L reads a little larger here than it stands on.</summary>
    private static string Footprint(JsonElement prop)
    {
        if (!prop.TryGetProperty("wings", out var wings) || wings.ValueKind != JsonValueKind.Array
            || wings.GetArrayLength() == 0) return "";

        double minX = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxZ = double.MinValue;
        foreach (var wing in wings.EnumerateArray())
        {
            if (!wing.TryGetProperty("corners", out var corners) || corners.ValueKind != JsonValueKind.Array) continue;
            foreach (var corner in corners.EnumerateArray())
            {
                if (corner.ValueKind != JsonValueKind.Array || corner.GetArrayLength() < 2) continue;
                var x = Math.Floor(corner[0].GetDouble());
                var z = Math.Floor(corner[1].GetDouble());
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                minZ = Math.Min(minZ, z); maxZ = Math.Max(maxZ, z);
            }
        }
        if (minX > maxX || minZ > maxZ) return "";
        return $"{(int)(maxX - minX) + 1} × {(int)(maxZ - minZ) + 1}";
    }

    private static bool Bool(JsonElement prop, string name)
        => prop.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static string Field(JsonElement element, string name, string fallback)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
           && value.ValueKind == JsonValueKind.String ? value.GetString() ?? fallback : fallback;

    private static double Number(JsonElement element, string name, double fallback)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
           && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : fallback;

    private static string Whole(double value) => Math.Round(value).ToString(CultureInfo.InvariantCulture);

    private static string Capital(string word)
        => word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..].Replace('_', ' ');

    /// <summary>A registry key read as words: <c>dark-oak-cottage</c> is <c>Dark oak cottage</c>.</summary>
    private static string Words(string key) => Capital(key.Replace('-', ' '));

    private static string RowElementId(string propId) => $"placed-{propId}";

    /// <summary>Fold or open a kind. A folded row cannot report the pointer leaving it, so the ring goes too.</summary>
    private void Toggle(string kind)
    {
        if (!collapsed.Remove(kind)) collapsed.Add(kind);
        Hover(null);
        skipRender = false;     // the fold itself is drawn
    }

    /// <summary>A row is reached by Tab and picked by Enter, as a click picks it.</summary>
    private Task OnRowKey(KeyboardEventArgs key, string id)
        => key.Key == "Enter" ? Select(id) : Task.CompletedTask;

    private Task Select(string id)
        => Handle is null ? Task.CompletedTask : Handle.InvokeVoidAsync("selectProp", id).AsTask();

    /// <summary>Ring the row's prop on the board while the pointer or focus is on the row. Only the board changes,
    /// so the list does not render for it, and the call is not awaited because an awaited handler renders again
    /// when it completes: a render re-runs the icon pass, which swaps glyphs out from under a press landing
    /// elsewhere — the dock button the pointer is leaving the row for.</summary>
    private void Hover(string? id)
    {
        skipRender = true;
        if (Handle is not null) _ = Handle.InvokeVoidAsync("hoverProp", id).AsTask();
    }

    private bool skipRender;

    protected override bool ShouldRender()
    {
        if (!skipRender) return true;
        skipRender = false;
        return false;
    }
}
