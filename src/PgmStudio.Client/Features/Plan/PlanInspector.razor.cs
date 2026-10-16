using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Features.Plan;

public partial class PlanInspector
{
    /// <summary>The bridge every edit is written through.</summary>
    [Parameter] public IJSObjectReference? Handle { get; set; }

    /// <summary>What the canvas has selected, or null for nothing.</summary>
    [Parameter] public PlanSelection? Selection { get; set; }

    /// <summary>The objective vocabulary and the defaults an unset marker field falls back to.</summary>
    [Parameter, EditorRequired] public ObjectiveVocabularyDto Vocabulary { get; set; } = default!;

    /// <summary>Blocks one surface click moves a piece. The host owns it: the Info phase edits the same
    /// preference, and the bridge clamps and persists it.</summary>
    [Parameter] public double SurfaceStep { get; set; }

    [Parameter] public EventCallback<double> OnSurfaceStep { get; set; }

    private static readonly int[] StepPresets = [1, 2, 3];

    private static IReadOnlyList<SelectOption> RoleOptions => PlanPalette.RoleOptions;

    private static IReadOnlyList<SelectOption> BoxKindOptions => PlanPalette.BoxKindOptions;

    private static string MarkerIcon(string kind) => PlanPalette.MarkerIcon(kind);

    private string OffsetLabel => Selection?.At is { Length: 2 } at ? $"{at[0]}, {at[1]}" : "";

    private Task Edit(string verb, params object?[] arguments)
        => Handle is not null ? Handle.InvokeVoidAsync(verb, arguments).AsTask() : Task.CompletedTask;

    private Task EditSelected(string verb, params object?[] arguments)
        => Selection is not null ? Edit(verb, [Selection.Id, .. arguments]) : Task.CompletedTask;

    // ── piece, zone and box edits ────────────────────────────────────────────────

    private Task OnPieceId(ChangeEventArgs change) => EditSelected("setPieceId", change.Value?.ToString() ?? "");

    private Task OnPieceRole(string role) => EditSelected("setPieceRole", role);

    private Task StepSurface(int delta) => EditSelected("stepPieceSurface", delta);

    private Task ToggleMirrors() => EditSelected("togglePieceMirrors");

    private Task OnZoneId(ChangeEventArgs change) => EditSelected("setZoneId", change.Value?.ToString() ?? "");

    private Task OnBoxId(ChangeEventArgs change) => EditSelected("setBoxId", change.Value?.ToString() ?? "");

    private Task OnBoxKind(string kind) => EditSelected("setBoxKind", kind);

    private Task ToggleBoxMembers() => EditSelected("toggleBoxMembers");

    private Task CycleFacing()
        => Selection is not null ? Edit("cycleFacing", Selection.Index) : Task.CompletedTask;

    private Task DeleteSelected() => Edit("deleteSelected");

    private Task SetSurfaceStep(int step) => OnSurfaceStep.InvokeAsync(step);

    // ── objective markers: the structure each one builds ─────────────────────────
    //
    // A core and a destroyable are placed as a bare marker and take the generator's defaults; these are the
    // knobs that vary them. What is stored is only what differs: setting a field back to its default passes
    // null, which removes the key, so a plan the author never varied stays the bare markers it was written as
    // and a default that later moves moves for every plan that never disagreed with it.

    private Task SetMarkerField(string key, object? value)
        => Selection is not null
            ? Edit("setMarkerField", Selection.MarkerKind, Selection.Index, key, value)
            : Task.CompletedTask;

    /// <summary>What the selected marker states, for the objective fields.</summary>
    private ObjectiveFieldValues MarkerValues => Selection is not { } marker ? new() : new(
        marker.Name, marker.Color, marker.Style, marker.Materials, marker.Float, marker.Lava, marker.LavaHeight,
        marker.OpenTop, marker.Leak);
}
