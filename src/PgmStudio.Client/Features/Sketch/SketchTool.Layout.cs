using Microsoft.JSInterop;
using PgmStudio.Client.Components;
using PgmStudio.Client.Features.Plan;
using PgmStudio.Geom;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>Draw's two steps: Ground, where the shapes are drawn, and Layout, where the play pieces are placed
/// from the plan editor's three toolbars. Both draw every piece and pick, drag and turn any of them.</summary>
public partial class SketchTool
{
    private static readonly string[] DrawSteps = ["Ground", "Layout"];
    private const int LayoutStep = 1;
    private int drawStep;
    private string? openFlyout;
    private string pieceActive = PlayPieceKinds.Spawn;
    private string zoneActive = PlayPieceKinds.BuildArea;
    private string markerActive = PlayPieceKinds.IronCube;

    private bool LayoutActive => DrawActive && drawStep == LayoutStep;

    private static string PieceTool(string kind) => $"piece:{kind}";

    private static readonly DockItem[] PieceItems =
    [
        new(PlayPieceKinds.Spawn, "Spawn piece", Swatch: "var(--canvas-role-spawn)"),
        new(PlayPieceKinds.WoolRoom, "Wool room", Swatch: "var(--canvas-role-wool-room)"),
    ];

    private static readonly DockItem[] ZoneItems =
    [
        new(PlayPieceKinds.BuildArea, "Build area", SwatchClass: "canvas-dock-swatch--build"),
        new(PlayPieceKinds.WaterLane, "Water lane", SwatchClass: "canvas-dock-swatch--water-lane"),
    ];

    /// <summary>The markers a board may place. A destroyable and a core are offered at two teams only, as the
    /// plan offers them (OB14).</summary>
    private DockItem[] MarkerItems =>
    [
        new(PlayPieceKinds.IronCube, "Iron marker", Icon: PlanPalette.MarkerIcon("iron")),
        .. Symmetry.Order(mode) == 2
            ? new DockItem[]
            {
                new(PlayPieceKinds.Destroyable, "Destroyable", Icon: PlanPalette.MarkerIcon("destroyable")),
                new(PlayPieceKinds.Core, "Core", Icon: PlanPalette.MarkerIcon("core")),
            }
            : [],
        new(PlayPieceKinds.Wall, "Wall", Icon: PlanPalette.MarkerIcon("wall")),
    ];

    private Task StepDraw(int step)
    {
        drawStep = step;
        openFlyout = null;
        return SetTool("select");
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
