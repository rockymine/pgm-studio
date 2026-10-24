using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>The Terraform phase's modifier stack, in the order it runs (<c>docs/tools/sketch.md</c>,
/// <i>Terraform</i>). Every change goes through the bridge, which holds the stack as the layout stores it.</summary>
public partial class SketchModifierList
{
    [Parameter] public IJSObjectReference? Handle { get; set; }
    [Parameter] public SketchModifierState State { get; set; } = SketchModifierState.Empty;
    [Inject] public IJSRuntime JS { get; set; } = default!;

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");

    private Task Add(string op) => Call("addModifier", op);

    private Task Pick(string id) => Call("selectModifier", id == State.SelectedId ? null : id);

    private Task Move(string id, int by) => Call("moveModifier", id, by);

    private Task Toggle(string id, bool off) =>
        Call("updateModifier", id, new JsonObject { ["off"] = off }.ToJsonString());

    private Task Call(string verb, params object?[] args) =>
        Handle is null ? Task.CompletedTask : Handle.InvokeVoidAsync(verb, args).AsTask();
}
