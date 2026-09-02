using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Components;

/// <summary>
/// The pictures of a building an editor has open, and which of them is showing. The three cuts are SVG the
/// preview call answered; the building standing up is drawn here, in WebGL, from the world's own columns.
/// </summary>
public partial class HouseViews : IAsyncDisposable
{
    /// <summary>The building standing up, drawn in 3-D and turnable.</summary>
    public const string Iso = "iso";

    /// <summary>The roof from above — its form, its hole, how far it oversails, and a porch's notch.</summary>
    public const string Plan = "plan";

    /// <summary>The course stack, cut open: what the walls are made of, band by band.</summary>
    public const string Section = "section";

    /// <summary>One plane at the scale of the pieces in it — the only view where a stair lattice is the
    /// opening it is, and where a storey's slab, the clear under it and the ladder through it read at once.</summary>
    public const string Cutaway = "cutaway";

    /// <summary>Everything: the building over a row of the three cuts.</summary>
    public const string All = "all";

    /// <summary>The views in the order they are offered, the overview first.</summary>
    public static readonly (string Id, string Label, string Icon, string Note)[] Offered =
    [
        (All, "All", "layout-grid", "The building over the three cuts"),
        (Iso, "3-D", "box", "The building standing up, turnable"),
        (Plan, "Plan", "grid", "The roof from above — its form, its hole, its overhang"),
        (Section, "Section", "layers", "The course stack, cut open"),
        (Cutaway, "Cutaway", "square-dashed", "One plane at the scale of the pieces in it"),
    ];

    [Parameter] public RoomStylePreviewDto? Preview { get; set; }

    /// <summary>Which view is showing; <see cref="All"/> shows every one.</summary>
    [Parameter] public string View { get; set; } = All;

    private ElementReference wrap;
    private IJSObjectReference? scene;
    private string? unavailable;

    /// <summary>What was last handed to the scene, so a re-render that changed nothing does not re-mesh.</summary>
    private WorldColumnsDto? drawn;

    /// <summary>Whether a mount is in flight. Mounting is awaited and a render can land while it is, so
    /// without this the second one starts a second scene and the first canvas is never reached again.</summary>
    private bool mounting;

    private bool Shows(string view) => View == All || View == view;

    /// <summary>The scene is mounted on the element, so it can only be built once the element exists — and it
    /// has to be torn down when the view moves off the 3-D one, because the element goes with it.</summary>
    protected override async Task OnAfterRenderAsync(bool first)
    {
        if (Preview is null || !Shows(Iso))
        {
            await Drop();
            return;
        }

        if (scene is null && unavailable is null)
        {
            if (mounting) return;
            mounting = true;
            try { scene = await JS.InvokeAsync<IJSObjectReference?>("studio.mountHouseIso", wrap); }
            finally { mounting = false; }

            if (scene is null)
            {
                unavailable = "This browser cannot draw the building in 3-D. The cuts beside it still read.";
                StateHasChanged();
                return;
            }
            drawn = null;
        }

        if (scene is null || ReferenceEquals(drawn, Preview.Columns)) return;
        drawn = Preview.Columns;
        // Shown first, so the world lands on a canvas that already has the size it will be drawn at.
        await scene.InvokeVoidAsync("show");
        await scene.InvokeVoidAsync("draw", Preview.Columns);
    }

    private async Task Rotate()
    {
        if (scene is not null) await scene.InvokeVoidAsync("rotate");
    }

    private async Task Drop()
    {
        if (scene is null) return;
        var going = scene;
        scene = null;
        drawn = null;
        await going.InvokeVoidAsync("dispose");
        await going.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        // The circuit is already gone when a page is navigated away from mid-teardown, and disposing a
        // reference into it throws rather than answering.
        try { await Drop(); }
        catch (JSDisconnectedException) { }
    }
}
