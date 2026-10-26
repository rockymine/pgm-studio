using Microsoft.JSInterop;
using PgmStudio.Client.Components;
using PgmStudio.Geom;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>Draw's three steps: Places, where the places are drawn; Ground, where the shapes are drawn; and Layout,
/// where the play pieces are placed from three toolbars. Ground and Layout draw every piece and pick, drag and turn
/// any of them.</summary>
public partial class SketchTool
{
    private static readonly string[] DrawSteps = ["Places", "Ground", "Layout"];
    private const int PlacesStep = 0;
    private const int GroundStep = 1;
    private const int LayoutStep = 2;
    private int drawStep = GroundStep;

    /// <summary>Whether the caller is an admin, who alone sees the Places step and the places.</summary>
    private bool placesAllowed;

    /// <summary>The first of Draw's steps the caller has: Places for an admin, else Ground.</summary>
    private int FirstDrawStep => placesAllowed ? PlacesStep : GroundStep;

    private IReadOnlyList<string> DrawStepNames => DrawSteps[FirstDrawStep..];
    private string? openFlyout;
    private string pieceActive = PlayPieceKinds.Spawn;
    private string zoneActive = PlayPieceKinds.BuildArea;
    private string markerActive = PlayPieceKinds.IronCube;

    private bool LayoutActive => DrawActive && drawStep == LayoutStep;

    private static string PieceTool(string kind) => $"piece:{kind}";

    private static readonly DockItem[] PieceItems =
    [
        new(PlayPieceKinds.Spawn, "Spawn room", Swatch: "var(--canvas-role-spawn)"),
        new(PlayPieceKinds.WoolRoom, "Wool room", Swatch: "var(--canvas-role-wool-room)"),
    ];

    private static readonly DockItem[] ZoneItems =
    [
        new(PlayPieceKinds.BuildArea, "Build region", SwatchClass: "canvas-dock-swatch--build"),
        new(PlayPieceKinds.WaterLane, "Water lane", SwatchClass: "canvas-dock-swatch--water-lane"),
    ];

    /// <summary>The markers a layout may place. A monument and a core are offered at two teams only (OB14); a
    /// capture point belongs to nobody and is offered at any.</summary>
    private DockItem[] MarkerItems =>
    [
        new(PlayPieceKinds.IronCube, "Iron cube", Icon: "pickaxe"),
        .. Symmetry.Order(mode) == 2
            ? new DockItem[]
            {
                new(PlayPieceKinds.Destroyable, "Monument", Icon: "gem"),
                new(PlayPieceKinds.Core, "Core", Icon: "flame"),
            }
            : [],
        new(PlayPieceKinds.ControlPoint, "Capture point", Icon: "flag"),
        new(PlayPieceKinds.Wall, "Wall", Icon: "brick-wall"),
        new(PlayPieceKinds.Chest, "Defence chest", Icon: "box"),
    ];

    private async Task StepDraw(int step)
    {
        drawStep = Math.Max(step, FirstDrawStep);
        openFlyout = null;
        await SetTool("select");
        await PushLayoutEditingAsync();
        await PushPlacesAsync();
    }

    private void ToggleFlyout(string family) => openFlyout = openFlyout == family ? null : family;

    private void CloseFlyouts() => openFlyout = null;

    private async Task ArmPiece(string kind)
    {
        openFlyout = null;
        if (PieceItems.Any(item => item.Key == kind)) pieceActive = kind;
        else if (ZoneItems.Any(item => item.Key == kind)) zoneActive = kind;
        else markerActive = kind;
        tool = PieceTool(kind);
        if (handle is not null) await handle.InvokeVoidAsync("armPiece", kind);
    }

    private Task DeleteStructural() =>
        selectedStructural is null || handle is null
            ? Task.CompletedTask
            : handle.InvokeVoidAsync("deleteStructural", selectedStructural.Id).AsTask();
}
