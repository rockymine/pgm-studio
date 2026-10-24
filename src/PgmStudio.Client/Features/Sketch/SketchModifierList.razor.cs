using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>The Terraform phase's left column: the drawn shapes with how the modifiers meet each, and the
/// modifier stack in the order it runs (<c>docs/tools/sketch.md</c>, <i>Terraform</i>). Every change goes through
/// the bridge, which holds the stack as the layout stores it.</summary>
public partial class SketchModifierList
{
    [Parameter] public IJSObjectReference? Handle { get; set; }
    [Parameter] public SketchModifierState State { get; set; } = SketchModifierState.Empty;

    /// <summary>The drawn shapes, whose relation to the stack each row sets.</summary>
    [Parameter] public IReadOnlyList<SketchShapeRow> Shapes { get; set; } = [];

    [Inject] public IJSRuntime JS { get; set; } = default!;

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");

    /// <summary>The shapes that are ground. A carve takes ground away and has none of its own to meet the stack.</summary>
    private IReadOnlyList<SketchShapeRow> Ground => [.. Shapes.Where(shape => shape.Operation != "subtract")];

    private static readonly IReadOnlyList<SelectOption> Relations =
    [
        new("", "Follow", "The modifiers reshape it with the land around it."),
        new(ReliefScopes.Hold, "Hold", "Stays at its own height; the modifiers leave it out."),
        new(ReliefScopes.Ride, "Ride",
            "Keeps its form and moves by the median change the modifiers made to the ground under it."),
    ];

    private static string Capitalised(string word) =>
        word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..];

    private static string HeightModeNote(string mode) =>
        $"{Capitalised(mode)}: it stands at its own height statement, so the modifiers leave it out.";

    private Task Relate(string id, string word) => Call("setReliefScope", id, word);

    private Task Pick(string id) => Call("selectModifier", id == State.SelectedId ? null : id);

    private Task Move(string id, int by) => Call("moveModifier", id, by);

    private Task Toggle(string id, bool off) =>
        Call("updateModifier", id, new JsonObject { ["off"] = off }.ToJsonString());

    private Task Call(string verb, params object?[] args) =>
        Handle is null ? Task.CompletedTask : Handle.InvokeVoidAsync(verb, args).AsTask();
}
