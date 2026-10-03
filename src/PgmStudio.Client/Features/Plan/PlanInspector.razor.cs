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

    /// <summary>Parse a picked number, keeping the current value when the pick is unreadable.</summary>
    private static int Num(object? value, int fallback)
        => int.TryParse(value?.ToString(), out var parsed) ? parsed : fallback;

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

    /// <summary>Store a number, or clear it when it equals the default it would fall back to anyway.</summary>
    private Task SetMarkerNumber(string key, int value, int fallback)
        => SetMarkerField(key, value == fallback ? null : value);

    private Task SetMarkerText(string key, string? value, string fallback)
        => SetMarkerField(key, string.IsNullOrWhiteSpace(value) || value.Trim() == fallback ? null : value.Trim());

    // The effective value of each knob: what the author set, else what the generator will use.
    private string DestroyableStyle => Selection?.Style ?? Vocabulary.Destroyable.Style;
    private string DestroyableMaterials => Selection?.Materials ?? Vocabulary.Destroyable.Materials;
    private int DestroyableFloat => Selection?.Float ?? Vocabulary.Destroyable.Float;

    private int CoreLava => Selection?.Lava ?? Vocabulary.Core.Lava;
    private int CoreLavaHeight => Selection?.LavaHeight ?? Vocabulary.Core.LavaHeight;
    private int CoreFloat => Selection?.Float ?? Vocabulary.Core.Float;
    private int CoreLeak => Selection?.Leak ?? Vocabulary.Core.Leak;
    private bool CoreOpenTop => Selection?.OpenTop ?? Vocabulary.Core.OpenTop;

    /// <summary>The obsidian the two stated numbers imply, so the author reads the structure they are
    /// building rather than the interior alone.</summary>
    private string CoreCasingReadout
    {
        get
        {
            var (size, height) = CoreCasing.Of(CoreLava, CoreLavaHeight, CoreOpenTop);
            return $"{size}×{size}×{height} obsidian, {CoreLava}×{CoreLava}×{CoreLavaHeight} lava inside";
        }
    }

    /// <summary>How far players must dig under the casing before its lava can leak — the whole point of the
    /// float/leak pair, which says nothing when either is read alone.</summary>
    private int CoreDigDepth => CoreDig.Depth(CoreLeak, CoreFloat);

    /// <summary>The dye a wool marker states, or empty where it states none. Unlike the other knobs there is
    /// no default to fall back to: an unstated colour is resolved at compile time against the marker's team and
    /// the wools before it, which the editor cannot know from one marker.</summary>
    private string WoolColor => Selection?.Color ?? "";

    private IReadOnlyList<SelectOption> WoolColorOptions
        => [.. Vocabulary.Wool.Colors.Select(dye => new SelectOption(dye.Name, dye.Label))];

    private IReadOnlyList<SelectOption> DestroyableStyleOptions
        => [.. Vocabulary.Destroyable.Styles.Select(design => new SelectOption(design, design))];

    private IReadOnlyList<SelectOption> DestroyableMaterialOptions
        => [.. Vocabulary.Destroyable.MaterialChoices.Select(material => new SelectOption(material, material))];

    private IReadOnlyList<SelectOption> LavaOptions
        => [.. Vocabulary.Core.LavaRange.Select(size => new SelectOption(size.ToString(), $"{size} × {size}"))];

    private IReadOnlyList<SelectOption> LavaHeightOptions
        => [.. Vocabulary.Core.LavaHeightRange.Select(height => new SelectOption(height.ToString(), height.ToString()))];

    /// <summary>The swatch beside the picker: the stated dye's own colour, or the neutral the auto option
    /// stands for, since no one colour is what "auto" resolves to.</summary>
    private string WoolSwatch
        => Vocabulary.Wool.Colors.FirstOrDefault(dye => dye.Name == WoolColor)?.Hex ?? "var(--border)";
}
