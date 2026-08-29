using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>
/// The Relief phase's readback panel: what the stated terrain costs a player, per group.
///
/// <para>Fetched on demand rather than pushed on every edit. It is a second pass of measurement over the same
/// solved field — reachability floods at two tiers, a face sweep, a crossing count each way — and an author
/// reads the board when they stop to think, not on every dragged vertex. The button is the whole
/// interaction.</para>
/// </summary>
public partial class SketchReliefReadback
{
    [Parameter] public IJSObjectReference? Handle { get; set; }
    /// <summary>Bumped by the host whenever the relief changes, so a reading taken before an edit does not sit
    /// on screen describing terrain that has moved.</summary>
    [Parameter] public int Revision { get; set; }

    [Inject] public IJSRuntime JS { get; set; } = default!;

    private sealed record TierRow(string Name, double Share, int Places, double LargestPlace, int Ledges,
        IReadOnlyList<PartRow> Parts);

    /// <summary>One piece of surface with the coordinates to find it at. A count says a board is broken; this
    /// says where to look, which is what an author needs before they can fix it.</summary>
    private sealed record PartRow(int Cells, double Share, int CentroidX, int CentroidZ, bool Place);
    private sealed record FaceRow(int Width, int Drop);
    private sealed record Crossing(int Rows, int OnFoot, int WithBlock, int Descended);
    private sealed record GroupRead(string Id, int Low, int High, int Relief, List<TierRow> Tiers,
        List<FaceRow> Faces, int FaceCount, int Cliffs, Crossing AcrossX, Crossing AcrossZ,
        int SymmetryError, bool Symmetric);

    private List<GroupRead> groups = [];
    private bool busy;
    private int readAt = -1;      // the revision the reading on screen describes

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");

    protected override void OnParametersSet()
    {
        // A reading of terrain that has since moved is worse than none — it reads as current and is not.
        if (Revision != readAt) groups = [];
    }

    private static string TierWord(string name) => name switch
    {
        "walk" => "a jump",
        "scramble" => "a placed block",
        _ => name,
    };

    private static string Pct(double share) => $"{share * 100:0.#}%";

    private async Task Measure()
    {
        if (Handle is null || busy) return;
        busy = true;
        try
        {
            var json = await Handle.InvokeAsync<string>("readRelief");
            groups = Parse(json);
            readAt = Revision;
        }
        catch { groups = []; }
        finally { busy = false; }
    }

    private static List<GroupRead> Parse(string json)
    {
        var read = new List<GroupRead>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("groups", out var array) || array.ValueKind != JsonValueKind.Array)
                return read;

            foreach (var group in array.EnumerateArray())
            {
                var tiers = new List<TierRow>();
                foreach (var tier in Array(group, "tiers"))
                {
                    var parts = new List<PartRow>();
                    foreach (var part in Array(tier, "parts"))
                        parts.Add(new PartRow(Int(part, "cells"), Dbl(part, "share"),
                            Int(part, "centroidX"), Int(part, "centroidZ"), Bool(part, "place")));
                    tiers.Add(new TierRow(Str(tier, "name"), Dbl(tier, "share"), Int(tier, "places"),
                        Dbl(tier, "largestPlace"), Int(tier, "ledges"), parts));
                }

                var faces = new List<FaceRow>();
                foreach (var face in Array(group, "faces")) faces.Add(new FaceRow(Int(face, "width"), Int(face, "drop")));

                var error = Int(group, "symmetryError");
                read.Add(new GroupRead(Str(group, "group"), Int(group, "low"), Int(group, "high"),
                    Int(group, "relief"), tiers, faces, Int(group, "faceCount"), Int(group, "cliffs"),
                    CrossingOf(group, "acrossX"), CrossingOf(group, "acrossZ"), error,
                    // "Mirrors exactly" is only worth saying when a symmetry was declared at all; a group on
                    // a map with none is not symmetric, it is simply not being asked to be.
                    Symmetric: error == 0 && group.TryGetProperty("symmetryError", out _)));
            }
        }
        catch (JsonException) { /* a reply the client cannot read shows nothing rather than throwing */ }
        return read;
    }

    private static Crossing CrossingOf(JsonElement group, string name)
        => group.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object
            ? new Crossing(Int(value, "rows"), Int(value, "onFoot"), Int(value, "withBlock"), Int(value, "descended"))
            : new Crossing(0, 0, 0, 0);

    private static IEnumerable<JsonElement> Array(JsonElement parent, string name)
        => parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray() : [];

    private static string Str(JsonElement parent, string name)
        => parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? "" : "";

    private static int Int(JsonElement parent, string name)
        => parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32() : 0;

    private static double Dbl(JsonElement parent, string name)
        => parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble() : 0;

    private static bool Bool(JsonElement parent, string name)
        => parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}
