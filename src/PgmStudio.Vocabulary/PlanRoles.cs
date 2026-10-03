namespace PgmStudio.Vocabulary;

/// <summary>The authorable piece roles. A piece is anonymous by default (<see cref="Piece"/>); meaning is
/// derived from the assembled graph. Two roles carry intent and are kept: <see cref="WoolRoom"/> (the room
/// region — red terrain seams, bedrock floor at export) and <see cref="Spawn"/> (the spawn region — iron
/// inside it auto-renews). Retired role names (<c>lane</c>/<c>hub</c>/<c>mid</c>/<c>connector</c>) and any
/// unknown value map to <see cref="Piece"/>.
///
/// <para>Roles split into two kinds. <b>Generating</b> roles (<see cref="Piece"/>/<see cref="WoolRoom"/>/
/// <see cref="Spawn"/>) produce terrain and participate in connectivity, gameplay, validation and export.
/// <b>Annotation</b> roles (the <see cref="Annotations"/> set) produce no terrain of their own and take no
/// part in connectivity. The one such role is <see cref="Buffer"/>, and it does reach the export, as the one
/// thing an annotation can say there: <b>this ground is not terrain</b>. It states negative space —
/// lane-to-lane spacing, the border reservation, and holes (a hole is an enclosed buffer) — which the
/// compiler carves out of the union outline that would otherwise fill it (<c>PlanVoids</c>). It
/// never takes terrain from a generating piece: over one it stays inert, so a buffer can declare a void but
/// never destroy ground. New non-generating roles are added by extending
/// <see cref="Annotations"/>.</para></summary>
public static class PlanRoles
{
    public const string Piece = "piece";
    public const string WoolRoom = "wool-room";
    public const string Spawn = "spawn";
    public const string Buffer = "buffer";

    /// <summary>The four a piece may carry, generating first, in the order a picker offers them.</summary>
    public static readonly string[] All = [Piece, Spawn, WoolRoom, Buffer];

    /// <summary>The non-generating annotation roles — marks that document intent (spacing, reserved gaps and
    /// holes via <see cref="Buffer"/>) rather than produce terrain. Extend this to add more.</summary>
    public static readonly IReadOnlySet<string> Annotations = new HashSet<string> { Buffer };

    /// <summary>True when the role is an annotation — never terrain of its own, never buildable, and outside
    /// the graph. A <see cref="Buffer"/> still reaches the export as negative space (see the type remarks).</summary>
    public static bool IsAnnotation(string? role) => role is not null && Annotations.Contains(role);

    /// <summary>True when the role produces terrain and participates in the graph/export (everything that is
    /// not an <see cref="Annotations">annotation</see>).</summary>
    public static bool IsGenerating(string? role) => !IsAnnotation(role);

    /// <summary>The canonical role for a raw (possibly legacy or empty) value: <c>wool-room</c>, <c>spawn</c>
    /// and the annotation <c>buffer</c> survive; everything else — including the retired
    /// <c>lane</c>/<c>hub</c>/<c>mid</c>/<c>connector</c> — is a plain piece.</summary>
    public static string Canonical(string? role) => role switch
    {
        WoolRoom => WoolRoom,
        Spawn => Spawn,
        Buffer => Buffer,
        _ => Piece,
    };
}
