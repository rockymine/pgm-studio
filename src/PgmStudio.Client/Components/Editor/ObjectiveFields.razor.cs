using Microsoft.AspNetCore.Components;
using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Components;

/// <summary>What an objective states, each null where it states nothing and takes the default.</summary>
public sealed record ObjectiveFieldValues(
    string? Name = null, string? Color = null, string? Style = null, string? Materials = null, int? Float = null,
    int? Lava = null, int? LavaHeight = null, bool? OpenTop = null, int? Leak = null);

/// <summary>One setting an author changed: its <see cref="PieceSettings"/> key, and the value, null where it goes
/// back to its default.</summary>
public readonly record struct ObjectiveFieldChange(string Key, object? Value);

public partial class ObjectiveFields
{
    /// <summary>What the objective is: <c>destroyable</c>, <c>core</c> or <c>wool</c>.</summary>
    [Parameter, EditorRequired] public string Kind { get; set; } = "";

    [Parameter, EditorRequired] public ObjectiveFieldValues Values { get; set; } = new();

    /// <summary>The objective vocabulary and the defaults an unset field falls back to.</summary>
    [Parameter, EditorRequired] public ObjectiveVocabularyDto Vocabulary { get; set; } = default!;

    [Parameter] public EventCallback<ObjectiveFieldChange> OnSet { get; set; }

    private Task Set(string key, object? value) => OnSet.InvokeAsync(new ObjectiveFieldChange(key, value));

    /// <summary>Store a number, or clear it when it equals the default it would fall back to anyway.</summary>
    private Task SetNumber(string key, int value, int fallback) => Set(key, value == fallback ? null : value);

    private Task SetText(string key, string? value, string fallback)
        => Set(key, string.IsNullOrWhiteSpace(value) || value.Trim() == fallback ? null : value.Trim());

    /// <summary>Parse a picked number, keeping the current value when the pick is unreadable.</summary>
    private static int Num(object? value, int fallback)
        => int.TryParse(value?.ToString(), out var parsed) ? parsed : fallback;

    // The effective value of each knob: what the author set, else what the generator will use.
    private string DestroyableStyle => Values.Style ?? Vocabulary.Destroyable.Style;
    private string DestroyableMaterials => Values.Materials ?? Vocabulary.Destroyable.Materials;
    private int DestroyableFloat => Values.Float ?? Vocabulary.Destroyable.Float;

    private int CoreLava => Values.Lava ?? Vocabulary.Core.Lava;
    private int CoreLavaHeight => Values.LavaHeight ?? Vocabulary.Core.LavaHeight;
    private int CoreFloat => Values.Float ?? Vocabulary.Core.Float;
    private int CoreLeak => Values.Leak ?? Vocabulary.Core.Leak;
    private bool CoreOpenTop => Values.OpenTop ?? Vocabulary.Core.OpenTop;

    /// <summary>The obsidian the two stated numbers imply, so the author reads the structure they are building
    /// rather than the interior alone.</summary>
    private string CoreCasingReadout
    {
        get
        {
            var (size, height) = CoreCasing.Of(CoreLava, CoreLavaHeight, CoreOpenTop);
            return $"{size}×{size}×{height} obsidian, {CoreLava}×{CoreLava}×{CoreLavaHeight} lava inside";
        }
    }

    /// <summary>How far players must dig under the casing before its lava can leak.</summary>
    private int CoreDigDepth => CoreDig.Depth(CoreLeak, CoreFloat);

    /// <summary>The dye the wool states, or empty where it states none.</summary>
    private string WoolColor => Values.Color ?? "";

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

    /// <summary>The swatch beside the picker: the stated dye's own colour, or the neutral the auto option stands
    /// for, since no one colour is what "auto" resolves to.</summary>
    private string WoolSwatch
        => Vocabulary.Wool.Colors.FirstOrDefault(dye => dye.Name == WoolColor)?.Hex ?? "var(--border)";
}
