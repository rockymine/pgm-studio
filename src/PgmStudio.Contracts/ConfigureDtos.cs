using System.Text.Json.Serialization;

namespace PgmStudio.Contracts;

/// <summary>
/// Where the Configure wizard has got to on one map (<c>GET /configure/{slug}/state</c>): what the scan was
/// told to read, and whether the author has settled the symmetry.
/// </summary>
/// <param name="ScanLayer">Which layer the world scan reads the ground off — <c>surface</c> unless the author
/// picked otherwise.</param>
/// <param name="ExcludeBlocks">Block ids the scan is told to ignore when it decides what ground is.</param>
/// <param name="ExcludeIslands">Detected islands the author has struck out, by their index in the stored
/// decomposition. Excluding one re-runs symmetry without re-reading the world.</param>
/// <param name="SymmetryStatus"><c>unconfirmed</c> until the author confirms or rejects the detection.</param>
/// <param name="ConfigureComplete">Whether the wizard is done, which is exactly whether the symmetry has been
/// settled — it is the last thing the wizard asks and nothing downstream can proceed without it.</param>
public sealed record ConfigureStateDto(
    [property: JsonPropertyName("scan_layer")] string ScanLayer,
    [property: JsonPropertyName("exclude_blocks")] IReadOnlyList<int> ExcludeBlocks,
    [property: JsonPropertyName("exclude_islands")] IReadOnlyList<int> ExcludeIslands,
    [property: JsonPropertyName("symmetry_status")] string SymmetryStatus,
    [property: JsonPropertyName("configure_complete")] bool ConfigureComplete);

/// <summary>
/// What an objective marker may be, and what each optional field is worth when the author leaves it unsaid
/// (<c>GET /objectives/vocabulary</c>).
///
/// <para>Served rather than duplicated on the client, because an inspector has to show the <b>effective</b>
/// value of every knob: a marker stores only what was changed, so an unset field must render as the number
/// the compiler and the stamper will actually use. A copy of these on the far side of the wire is the drift
/// this exists to prevent — a client offering float 4 while the stamper builds at 6 shows the author a
/// structure at a height nothing asked for.</para>
///
/// <para>Map-independent: the plan editor opens without a map.</para>
/// </summary>
public sealed record ObjectiveVocabularyDto(
    DestroyableVocabularyDto Destroyable,
    CoreVocabularyDto Core);

/// <summary>The destroyable designs on offer and what an unauthored one is made of.</summary>
/// <param name="Styles">The shapes a destroyable can be built as.</param>
/// <param name="MaterialChoices">The blocks it can be built from.</param>
/// <param name="Float">How far above the ground it stands. A destroyable on the ground is trivially covered,
/// which is why the default is not zero.</param>
public sealed record DestroyableVocabularyDto(
    IReadOnlyList<string> Styles,
    IReadOnlyList<string> MaterialChoices,
    string Style,
    string Materials,
    int Float);

/// <summary>What an unauthored core is: its casing, its shell, how far it floats, and how far its lava may
/// fall before the core counts as leaked. A core resting on the ground cannot leak, which is what
/// <see cref="Float"/> is for.</summary>
public sealed record CoreVocabularyDto(
    int Size, int Height, int Shell, int Float, int Leak, bool OpenTop);

/// <summary>
/// The symmetry of a map's islands (<c>GET /map/{slug}/symmetry</c>) — detected on first read and cached, then
/// confirmed or rejected by the author in the Configure wizard.
/// </summary>
/// <param name="Status"><c>unconfirmed</c> until the author has ruled on it.</param>
/// <param name="Modes">Every mode the detector scored, as it scored them.</param>
/// <param name="Center">The point the map folds about, absent where none was found.</param>
/// <param name="CenterCell">Whether each axis folds through one cell or between two — <c>1x2</c> and its
/// siblings — which decides whether a mirrored coordinate lands on a block or on a seam.</param>
/// <param name="Primary">The mode the map is taken to have, with the confidence behind it.</param>
public sealed record SymmetryDto(
    string Status,
    IReadOnlyList<SymmetryModeDto> Modes,
    SymmetryCenterDto? Center,
    [property: JsonPropertyName("center_cell")] string? CenterCell,
    SymmetryPrimaryDto? Primary);

/// <summary>One mode the detector scored: whether the islands fold that way and how strongly. Every mode is
/// reported, detected or not, so a caller sees what was ruled out as well as what was found.</summary>
public sealed record SymmetryModeDto(string Type, bool Detected, double Confidence);

/// <summary>The point a map folds about, in world coordinates. Half-integers are ordinary: a fold between two
/// blocks sits on the seam rather than on either.</summary>
public sealed record SymmetryCenterDto(double Cx, double Cz);

/// <summary>The mode a map is taken to have. <paramref name="UserOverride"/> is present only when the author
/// set it by hand, which is what distinguishes a confirmed detection from a corrected one.</summary>
public sealed record SymmetryPrimaryDto(
    string Type,
    double Confidence,
    [property: JsonPropertyName("user_override"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? UserOverride = null);

/// <summary>Rule on the symmetry the detector proposed, or state the one the author knows. Every field is
/// optional and only the stated ones are written, so confirming a detection and moving its centre are two
/// calls or one.</summary>
/// <param name="Status"><c>confirmed</c> to accept the symmetry, <c>none</c> to say the map has none — which
/// also clears the mode. Any other word is ignored.</param>
/// <param name="ConfirmedType">The mode the author states — <c>mirror_x</c>, <c>mirror_z</c>, <c>rot_180</c>
/// or <c>rot_90</c>. Stating it marks the symmetry as the author's rather than the detector's, at full
/// confidence; a word outside the four is refused as <c>RQ1</c>.</param>
/// <param name="Cx">Where the map folds, east–west. Stating either coordinate writes both, the unstated one
/// keeping what it was.</param>
/// <param name="Cz">Where the map folds, north–south.</param>
public sealed record SymmetryPatchRequest(
    string? Status = null,
    [property: JsonPropertyName("confirmed_type")] string? ConfirmedType = null,
    double? Cx = null,
    double? Cz = null);

/// <summary>Take a landmass out of the map, or put it back. An excluded island is left out of the footprint
/// the wizard works from and out of symmetry detection, so the cached detection is dropped and step 3
/// recomputes.</summary>
/// <param name="IslandId">The island, as <c>GET /map/{slug}/islands</c> numbers it.</param>
/// <param name="Excluded">Whether it is left out. Both values are ordinary: this is the one call that
/// reverses itself.</param>
public sealed record ExcludeIslandRequest(
    [property: JsonPropertyName("island_id")] int IslandId,
    bool Excluded);
