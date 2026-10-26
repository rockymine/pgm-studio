namespace PgmStudio.Client.Models;

/// <summary>
/// Friendly UI wording for a symmetry mode — the single source of the human-readable labels shared by the
/// Configure surfaces that display a detected or chosen symmetry (World scan/symmetry phases, the
/// Configure landing, the Configure activity, the Teams suggestion). Presentation only: the orbit
/// <em>count</em> is not duplicated here — call <see cref="PgmStudio.Geom.Symmetry.Order"/> for that.
/// </summary>
public static class SymmetryInfo
{
    /// <summary>The name of a symmetry mode, short enough for an option (e.g. "Mirror X"); unknown/none → "No symmetry".</summary>
    public static string Label(string? mode) => mode switch
    {
        "rot_90" => "Rotate 90°",
        "rot_180" => "Rotate 180°",
        "mirror_x" => "Mirror X",
        "mirror_z" => "Mirror Z",
        "mirror_d1" => "Mirror ╲",
        "mirror_d2" => "Mirror ╱",
        _ => "No symmetry",
    };

    /// <summary>What a symmetry mode does, as an option's note; none where the name says it.</summary>
    public static string? Note(string? mode) => mode switch
    {
        "rot_90" => "Four quarters turn into each other.",
        "rot_180" => "Two halves turn into each other.",
        "mirror_x" => "The left and right halves mirror each other.",
        "mirror_z" => "The front and back halves mirror each other.",
        "mirror_d1" or "mirror_d2" => "The halves mirror each other across the diagonal.",
        _ => null,
    };

    /// <summary>A short inline label for prose (e.g. "90° rotational", "mirror"); unknown/none → "no".</summary>
    public static string ShortLabel(string? mode) => mode switch
    {
        "rot_90" => "90° rotational",
        "rot_180" => "180° rotational",
        "mirror_x" or "mirror_z" or "mirror_d1" or "mirror_d2" => "mirror",
        _ => "no",
    };
}
