using Microsoft.AspNetCore.Components;
using PgmStudio.Client.Components;
using PgmStudio.Contracts;

using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>
/// What a picked plan piece states, and the one thing about it an author may correct. A spawn or wool room's
/// region carries the height it was compiled at — the plan's flat surface, which every recompile writes again
/// until a correction is stated — and a building footprint carries none, the region it stands in being what a
/// group's relief is held against.
/// </summary>
public partial class SketchStructuralInspector
{
    [Parameter, EditorRequired] public SketchStructuralRow Piece { get; set; } = default!;

    /// <summary>The corrected height, in blocks. The host writes the number and the author's-height flag
    /// together through the bridge, which is what makes the correction outlive the next recompile.</summary>
    [Parameter] public EventCallback<double> OnSetHeight { get; set; }

    /// <summary>Why the last drag on this piece was not made — the intent's own sentence, absent where the
    /// move went through. The intent owns where a room is, so a refusal is an answer rather than a fault.</summary>
    [Parameter] public string? Note { get; set; }

    [Parameter] public EventCallback OnClose { get; set; }

    /// <summary>What a goal or a wool states, read from the intent; null while it loads or for any other piece.</summary>
    [Parameter] public ObjectiveFieldValues? Settings { get; set; }

    /// <summary>The objective vocabulary the settings are offered from.</summary>
    [Parameter] public ObjectiveVocabularyDto? Vocabulary { get; set; }

    /// <summary>The way a spawn's players face on arriving, in degrees; null for any other piece.</summary>
    [Parameter] public double? Yaw { get; set; }

    [Parameter] public EventCallback<ObjectiveFieldChange> OnSetting { get; set; }

    /// <summary>Turn a spawn's facing a quarter clockwise.</summary>
    [Parameter] public EventCallback OnFace { get; set; }

    /// <summary>The objective fields' word for the piece's kind.</summary>
    private string ObjectiveKind => Piece.Role == PlayPieceKinds.WoolRoom ? "wool" : Piece.Role;

    /// <summary>Delete the piece and its images; offered only where pieces are added, which is Layout.</summary>
    [Parameter] public Func<Task>? OnDelete { get; set; }

    /// <summary>Turn the piece a quarter at a time: 1 clockwise seen from above, -1 the other way.</summary>
    [Parameter] public EventCallback<int> OnTurn { get; set; }

    private string Icon => Piece.Role switch
    {
        PlayPieceKinds.Destroyable => "gem",
        PlayPieceKinds.Core => "flame",
        PlayPieceKinds.IronCube => "pickaxe",
        PlayPieceKinds.Wall => "brick-wall",
        PlayPieceKinds.Chest => "box",
        PlayPieceKinds.Observer => "eye",
        PlayPieceKinds.SpawnPoint or PlayPieceKinds.WoolSpawn => "crosshair",
        StructuralRoles.Building => "house",
        _ => "square-dashed",
    };

    private string Label => Piece.Role switch
    {
        StructuralRoles.Spawn => "Spawn region",
        StructuralRoles.WoolRoom => "Wool room",
        StructuralRoles.Building => "Building",
        PlayPieceKinds.Destroyable => "Destroyable",
        PlayPieceKinds.Core => "Core",
        PlayPieceKinds.BuildArea => "Build area",
        PlayPieceKinds.WaterLane => "Water lane",
        PlayPieceKinds.IronCube => "Iron marker",
        PlayPieceKinds.Wall => "Wall",
        PlayPieceKinds.Chest => "Defence chest",
        PlayPieceKinds.Observer => "Observer",
        PlayPieceKinds.SpawnPoint => "Spawn point",
        PlayPieceKinds.WoolSpawn => "Wool spawn",
        _ => Piece.Role,
    };

    private Task HeightChanged(double value) => OnSetHeight.InvokeAsync(value);
}
