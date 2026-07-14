namespace PgmStudio.Pgm.Evaluate;

/// <summary>The semantic role of a piece of evidence — a styling table maps tag → colour/weight once,
/// globally, so every renderer stays generic. A free string (not an enum) so slot-relation rules can tag with
/// the <c>slot:*</c> convention (e.g. <c>slot:entry</c>) without a schema change.</summary>
public static class EvidenceTags
{
    public const string Offender = "offender";   // the geometry that broke the rule
    public const string Bound = "bound";         // the limit it should have respected
    public const string Measure = "measure";     // a measured quantity (a dimension line)
    public const string Context = "context";     // nearby geometry that frames the violation
}

/// <summary>
/// Drawable evidence for a <see cref="Violation"/> — cell-space geometry a generic renderer paints on the grid,
/// so a rule can be <i>seen</i>, not only read. Four primitives cover essentially every geometric rule; a term
/// returns them (it never draws), and the rule cards, the editor overlay, and the reject inspector are all
/// generic passes over this data. Attached the moment a term computes its judgement — the geometry is already
/// in hand.
/// </summary>
public abstract record Evidence(string Tag);

public sealed record EvidenceRect(string Tag, int[] Rect) : Evidence(Tag);

public sealed record EvidenceSegment(string Tag, double X1, double Z1, double X2, double Z2) : Evidence(Tag);

public sealed record EvidenceMarker(string Tag, double X, double Z) : Evidence(Tag);

public sealed record EvidenceMeasure(string Tag, double X1, double Z1, double X2, double Z2, string Label)
    : Evidence(Tag);

/// <summary>Terse factories for the four <see cref="Evidence"/> primitives, so a term attaches evidence in one
/// line while its geometry is in hand.</summary>
public static class Ev
{
    /// <summary>A cell-space rectangle <c>[x, z, w, h]</c> — the same shape a plan piece/zone rect carries.</summary>
    public static EvidenceRect Rect(string tag, int[] rect) => new(tag, rect);

    /// <summary>A cell-space line segment (two endpoints).</summary>
    public static EvidenceSegment Segment(string tag, double x1, double z1, double x2, double z2) =>
        new(tag, x1, z1, x2, z2);

    /// <summary>A cell-space point.</summary>
    public static EvidenceMarker Marker(string tag, double x, double z) => new(tag, x, z);

    /// <summary>A dimension line carrying a human label (e.g. <c>"25 &gt; 20"</c>); tagged <c>measure</c>.</summary>
    public static EvidenceMeasure Measure(double x1, double z1, double x2, double z2, string label) =>
        new(EvidenceTags.Measure, x1, z1, x2, z2, label);
}
