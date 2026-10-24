using Microsoft.AspNetCore.Components;
using PgmStudio.Client.Components;
using PgmStudio.Contracts;

using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>
/// What a picked play piece states, and what about it an author may correct. A spawn or wool room's region
/// carries the height its ground is held at, and a building footprint carries none, the region it stands in
/// being what its ground is.
/// </summary>
public partial class SketchStructuralInspector
{
    [Parameter, EditorRequired] public SketchStructuralRow Piece { get; set; } = default!;

    /// <summary>The corrected height, in blocks. The host writes the number and the author's-height flag
    /// together through the bridge.</summary>
    [Parameter] public EventCallback<double> OnSetHeight { get; set; }

    /// <summary>Why the last drag on this piece was not made — the intent's own sentence, absent where the
    /// move went through. The intent owns where a room is, so a refusal is an answer rather than a fault.</summary>
    [Parameter] public string? Note { get; set; }

    /// <summary>What a goal or a wool states, read from the intent; null while it loads or for any other piece.</summary>
    [Parameter] public ObjectiveFieldValues? Settings { get; set; }

    /// <summary>The objective vocabulary the settings are offered from.</summary>
    [Parameter] public ObjectiveVocabularyDto? Vocabulary { get; set; }

    /// <summary>The way a spawn's players face on arriving, in degrees; null for any other piece.</summary>
    [Parameter] public double? Yaw { get; set; }

    [Parameter] public EventCallback<ObjectiveFieldChange> OnSetting { get; set; }

    /// <summary>The house of a room whose wing is picked, shaped here and on the canvas; null where the piece is
    /// not one, or outside Layout.</summary>
    [Parameter] public RenderFragment? House { get; set; }

    /// <summary>Whether the canvas reshapes the piece here — a wall's ends drag apart, a region, an area or a lane
    /// resizes — which is Layout.</summary>
    [Parameter] public bool Reshapes { get; set; }

    /// <summary>Turn a spawn's facing a quarter clockwise.</summary>
    [Parameter] public EventCallback OnFace { get; set; }

    /// <summary>The objective fields' word for the piece's kind.</summary>
    private string ObjectiveKind => Piece.Role == PlayPieceKinds.WoolRoom ? "wool" : Piece.Role;

    /// <summary>Delete the piece and its images; offered only where pieces are added, which is Layout.</summary>
    [Parameter] public Func<Task>? OnDelete { get; set; }

    /// <summary>Turn the piece a quarter at a time: 1 clockwise seen from above, -1 the other way.</summary>
    [Parameter] public EventCallback<int> OnTurn { get; set; }

    private Task HeightChanged(double value) => OnSetHeight.InvokeAsync(value);
}
