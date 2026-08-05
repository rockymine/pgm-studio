using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>
/// The Dressing phase's sidebar: every prop on the map, in the order it was placed. The canvas is where a prop
/// is normally reached, but a marker in a dense corner is a small target and a list is the only view that says
/// how much has been placed at all — the same job the island tree does for shapes.
/// </summary>
public partial class SketchDressingList
{
    [Parameter] public IJSObjectReference? Handle { get; set; }
    /// <summary>The dressing state the bridge pushes (<c>OnDressing</c>).</summary>
    [Parameter] public string? StateJson { get; set; }
    [Inject] public IJSRuntime JS { get; set; } = default!;

    private sealed record Row(string Id, string Icon, string Label, string Where);

    private List<Row> rows = [];
    private string? selectedId;

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");

    protected override void OnParametersSet()
    {
        rows = [];
        selectedId = null;
        if (string.IsNullOrWhiteSpace(StateJson)) return;

        try
        {
            using var doc = JsonDocument.Parse(StateJson);
            var root = doc.RootElement;
            if (root.TryGetProperty("selectedId", out var selected) && selected.ValueKind == JsonValueKind.String)
                selectedId = selected.GetString();
            if (!root.TryGetProperty("props", out var props) || props.ValueKind != JsonValueKind.Array) return;

            foreach (var prop in props.EnumerateArray()) rows.Add(RowOf(prop));
        }
        catch (JsonException) { /* a state push the client cannot read lists nothing rather than throwing */ }
    }

    // A prop's line says what it is and where — a marker by its cell, an area by how far it runs, since those
    // are the two questions a list of placed things is asked.
    private static Row RowOf(JsonElement prop)
    {
        var kind = prop.TryGetProperty("kind", out var k) ? k.GetString() ?? "" : "";
        var id = prop.TryGetProperty("id", out var i) ? i.GetString() ?? "" : "";
        return kind switch
        {
            "tree" => new Row(id, "trees", Species(prop), Cell(prop)),
            "boulder" => new Row(id, "mountain", $"{Field(prop, "form", "round")} boulder", Cell(prop)),
            "path" => new Row(id, "spline", $"{Field(prop, "style", "solid")} path", Span(prop)),
            "water" => new Row(id, "droplet", $"{Field(prop, "form", "canal")} channel", Span(prop)),
            "flora" => new Row(id, "flower", "ground cover", Span(prop)),
            _ => new Row(id, "shapes", kind, ""),
        };
    }

    private static string Species(JsonElement prop) => Field(prop, "species", "oak");

    private static string Field(JsonElement prop, string name, string fallback)
        => prop.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback : fallback;

    private static string Cell(JsonElement prop)
        => prop.TryGetProperty("x", out var x) && prop.TryGetProperty("z", out var z)
            ? $"{x.GetRawText()}, {z.GetRawText()}" : "";

    private static string Span(JsonElement prop)
    {
        if (!prop.TryGetProperty("points", out var points) || points.ValueKind != JsonValueKind.Array) return "";
        var count = points.GetArrayLength();
        return count == 0 ? "" : $"{count} pts";
    }

    private Task Select(string id)
        => Handle is null ? Task.CompletedTask : Handle.InvokeVoidAsync("selectProp", id).AsTask();
}
