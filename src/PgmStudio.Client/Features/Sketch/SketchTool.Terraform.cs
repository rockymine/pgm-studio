using Microsoft.JSInterop;
using PgmStudio.Client.Components;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>Terraform's dock: the operations a modifier is traced with, in four families, and the step the
/// ground is read at (<c>docs/tools/sketch.md</c>, <i>Terraform</i>).</summary>
public partial class SketchTool
{
    private const string GroundFamily = "ground", LinesFamily = "lines", WaterFamily = "water", TextureFamily = "texture";

    private static DockItem[] ModifierItems(params string[] ops) =>
        [.. ops.Select(op => new DockItem(op, ModifierOps.Label(op), Icon: SketchModifierState.Icon(op)))];

    private static readonly DockItem[] GroundItems =
        ModifierItems(ModifierOps.Mound, ModifierOps.Crater, ModifierOps.Level, ModifierOps.Butte, ModifierOps.Spire);

    private static readonly DockItem[] LineItems =
        ModifierItems(ModifierOps.Ridge, ModifierOps.Scarp, ModifierOps.Canyon, ModifierOps.Road);

    private static readonly DockItem[] WaterItems = ModifierItems(ModifierOps.River, ModifierOps.Lake);

    private static readonly DockItem[] TextureItems =
        ModifierItems(ModifierOps.Noise, ModifierOps.Gullies, ModifierOps.Warp);

    private string groundActive = ModifierOps.Mound;
    private string linesActive = ModifierOps.Ridge;
    private string waterActive = ModifierOps.River;
    private string textureActive = ModifierOps.Noise;

    private bool ModifierArmed(string op) => tool == SketchModifierState.Tool(op);

    /// <summary>Puts the picked operation in its family's slot and arms its tracing tool.</summary>
    private async Task ArmModifier(string op)
    {
        openFlyout = null;
        if (GroundItems.Any(item => item.Key == op)) groundActive = op;
        else if (LineItems.Any(item => item.Key == op)) linesActive = op;
        else if (WaterItems.Any(item => item.Key == op)) waterActive = op;
        else textureActive = op;
        await SetTool(SketchModifierState.Tool(op));
    }

    /// <summary>Reads the ground after <paramref name="count"/> modifiers; null reads it after all of them.</summary>
    private Task SetStep(int? count) =>
        handle is null ? Task.CompletedTask : handle.InvokeVoidAsync("setStep", count).AsTask();
}
