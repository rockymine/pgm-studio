using Microsoft.AspNetCore.Components;

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

    [Parameter] public EventCallback OnClose { get; set; }

    private string Label => Piece.Role switch
    {
        StructuralRoles.Spawn => "spawn region",
        StructuralRoles.WoolRoom => "wool room",
        StructuralRoles.Building => "building footprint",
        _ => Piece.Role,
    };

    private Task HeightChanged(double value) => OnSetHeight.InvokeAsync(value);
}
