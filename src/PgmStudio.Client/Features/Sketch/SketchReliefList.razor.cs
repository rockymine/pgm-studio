using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>
/// The Relief phase's sidebar: every mark on the map, grouped by the island stating it. The canvas is where a
/// mark is normally reached, but a spot height sitting inside a broad area is a small target and a list is the
/// only view that says how much of the terrain is authored at all.
///
/// <para>Grouped rather than flat, because the grouping is the model: a relief is solved over one island's
/// fused footprint, so two marks in different islands never talk to each other however close they look on the
/// canvas. Each group's header carries the island's own base, which is the level its marks are read against.</para>
/// </summary>
public partial class SketchReliefList
{
    [Parameter] public IJSObjectReference? Handle { get; set; }
    /// <summary>The relief state the bridge pushes (<c>OnRelief</c>).</summary>
    [Parameter] public string? StateJson { get; set; }
    [Inject] public IJSRuntime JS { get; set; } = default!;

    private sealed record Row(string Id, string Icon, string Label, string Where);
    private sealed record Group(string IslandId, string Base, List<Row> Marks);

    private List<Group> groups = [];
    private string? selectedId;

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");

    protected override void OnParametersSet()
    {
        groups = [];
        selectedId = null;
        if (string.IsNullOrWhiteSpace(StateJson)) return;

        try
        {
            using var doc = JsonDocument.Parse(StateJson);
            var root = doc.RootElement;
            if (root.TryGetProperty("selectedId", out var selected) && selected.ValueKind == JsonValueKind.String)
                selectedId = selected.GetString();
            if (!root.TryGetProperty("marks", out var marks) || marks.ValueKind != JsonValueKind.Array) return;

            // The push carries the base of the island in play only, so every other group falls back to a dash
            // rather than to a number that would be the wrong island's.
            var inPlay = root.TryGetProperty("islandId", out var island) ? island.GetString() : null;
            var inPlayBase = root.TryGetProperty("relief", out var relief) && relief.ValueKind == JsonValueKind.Object
                && relief.TryGetProperty("base", out var baseValue) ? baseValue.GetRawText() : null;

            var byIsland = new Dictionary<string, Group>();
            foreach (var mark in marks.EnumerateArray())
            {
                var islandId = mark.TryGetProperty("islandId", out var owner) ? owner.GetString() ?? "" : "";
                if (!byIsland.TryGetValue(islandId, out var group))
                    byIsland[islandId] = group = new Group(islandId, islandId == inPlay ? inPlayBase ?? "—" : "—", []);
                group.Marks.Add(RowOf(mark));
            }
            groups = [.. byIsland.Values];
        }
        catch (JsonException) { /* a state push the client cannot read lists nothing rather than throwing */ }
    }

    // A mark's line says what it states and how much ground it covers — the two questions a list of stated
    // terrain is asked. The height leads, because a relief is a set of heights before it is a set of shapes.
    private static Row RowOf(JsonElement mark)
    {
        var kind = mark.TryGetProperty("kind", out var k) ? k.GetString() ?? "" : "";
        var id = mark.TryGetProperty("id", out var i) ? i.GetString() ?? "" : "";
        return kind switch
        {
            "point" => new Row(id, "dot", $"spot at {Heights(mark)}", Cell(mark)),
            "line" => new Row(id, "spline", $"ridgeline at {Heights(mark)}", Span(mark, "points")),
            "area" => new Row(id, "pentagon", $"bench at {Heights(mark)}", Span(mark, "ring")),
            "scarp" => new Row(id, "triangle", $"scarp {Drop(mark)}", Span(mark, "points")),
            "rim" => new Row(id, "square-dashed", $"rim at {Heights(mark)}", "outline"),
            _ => new Row(id, "shapes", kind, ""),
        };
    }

    /// <summary>The heights a mark states. A falling ridgeline shows both ends, because one number would hide
    /// exactly what a per-vertex height exists to say.</summary>
    private static string Heights(JsonElement mark)
    {
        if (!mark.TryGetProperty("h", out var stated)) return "—";
        if (stated.ValueKind != JsonValueKind.Array) return stated.GetRawText();
        var values = stated.EnumerateArray().Select(value => value.GetRawText()).ToList();
        if (values.Count == 0) return "—";
        return values[0] == values[^1] ? values[0] : $"{values[0]}–{values[^1]}";
    }

    private static string Drop(JsonElement mark)
        => mark.TryGetProperty("high", out var high) && mark.TryGetProperty("low", out var low)
            ? $"{high.GetRawText()} ↓ {low.GetRawText()}" : "";

    private static string Cell(JsonElement mark)
        => mark.TryGetProperty("at", out var at) && at.ValueKind == JsonValueKind.Array && at.GetArrayLength() >= 2
            ? $"{at[0].GetRawText()}, {at[1].GetRawText()}" : "";

    private static string Span(JsonElement mark, string field)
    {
        if (!mark.TryGetProperty(field, out var points) || points.ValueKind != JsonValueKind.Array) return "";
        var count = points.GetArrayLength();
        return count == 0 ? "" : $"{count} pts";
    }

    private Task Select(string id)
        => Handle is null ? Task.CompletedTask : Handle.InvokeVoidAsync("selectMark", id).AsTask();

    /// <summary>Pick the island a group heads. It is the unit a relief is solved over, so selecting it is how
    /// its base, grain and rim are reached — and picking one clears the mark selection, since the inspector's
    /// island settings are about the island rather than about anything inside it.</summary>
    private async Task SelectIsland(string id)
    {
        if (Handle is null) return;
        await Handle.InvokeVoidAsync("selectMark", "");
        await Handle.InvokeVoidAsync("selectIsland", id);
    }
}
