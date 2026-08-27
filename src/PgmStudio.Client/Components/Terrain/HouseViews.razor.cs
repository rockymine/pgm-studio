using Microsoft.AspNetCore.Components;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Components;

/// <summary>
/// The pictures of a building an editor has open, and which of them is showing.
/// </summary>
public partial class HouseViews
{
    /// <summary>The isometric: what the building looks like standing up.</summary>
    public const string Iso = "iso";

    /// <summary>The roof from above — its form, its hole, how far it oversails, and a porch's notch.</summary>
    public const string Plan = "plan";

    /// <summary>The course stack, cut open: what the walls are made of, band by band.</summary>
    public const string Section = "section";

    /// <summary>One plane at the scale of the pieces in it — the only view where a stair lattice is the
    /// opening it is, and where a storey's slab, the clear under it and the ladder through it read at once.</summary>
    public const string Cutaway = "cutaway";

    /// <summary>Everything: the isometric over a row of the three cuts.</summary>
    public const string All = "all";

    /// <summary>The views in the order they are offered, the overview first.</summary>
    public static readonly (string Id, string Label, string Icon, string Note)[] Offered =
    [
        (All, "All", "layout-grid", "The isometric over the three cuts"),
        (Iso, "3-D", "box", "What the building looks like standing up"),
        (Plan, "Plan", "grid", "The roof from above — its form, its hole, its overhang"),
        (Section, "Section", "layers", "The course stack, cut open"),
        (Cutaway, "Cutaway", "square-dashed", "One plane at the scale of the pieces in it"),
    ];

    [Parameter] public RoomStylePreviewDto? Preview { get; set; }

    /// <summary>Which view is showing; <see cref="All"/> shows every one.</summary>
    [Parameter] public string View { get; set; } = All;

    private bool Shows(string view) => View == All || View == view;
}
