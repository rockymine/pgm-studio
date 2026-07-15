namespace PgmStudio.Client.Models;

/// <summary>
/// Friendly UI wording for a symmetry mode — the single source of the human-readable labels shared by the
/// Configure/Edit surfaces that display a detected or chosen symmetry (World scan/symmetry phases, the
/// Configure landing, the Configure activity, the Teams suggestion). Presentation only: the orbit
/// <em>count</em> is not duplicated here — call <see cref="PgmStudio.Geom.Symmetry.Order"/> for that.
/// </summary>
public static class SymmetryInfo
{
    /// <summary>The full friendly label for a symmetry mode (e.g. "Mirror X (left/right)"); unknown/none → "No symmetry".</summary>
    public static string Label(string? mode) => mode switch
    {
        "rot_90" => "Rotate 90°",
        "rot_180" => "Rotate 180°",
        "mirror_x" => "Mirror X (left/right)",
        "mirror_z" => "Mirror Z (front/back)",
        "mirror_d1" => "Mirror ╲ (diagonal)",
        "mirror_d2" => "Mirror ╱ (diagonal)",
        _ => "No symmetry",
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
