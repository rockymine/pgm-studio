using System.Text.Json.Serialization;

namespace PgmStudio.Contracts;

/// <summary>
/// One monument the scan thinks is there (<c>GET /map/{slug}/monument-suggestions</c>), highest confidence
/// first — a proposal for an author to confirm, never a decision.
///
/// <para><see cref="X"/>/<see cref="Y"/>/<see cref="Z"/> is the <b>air</b> block the wool is placed into, not
/// the pedestal under it: what the author confirms is where the monument goes, and the pedestal is the
/// evidence it was found by. <see cref="Source"/> says which evidence carried it — a <c>sign</c> naming the
/// colour, an <c>armorstand</c>, or bare <c>geometry</c> — and <see cref="Color"/> is the wool colour
/// inferred from it, absent where nothing named one.</para>
/// </summary>
/// <param name="Sign">Where the naming sign stands, when a sign is what found it.</param>
/// <param name="Evidence">The reasoning in the detector's own words, for an author deciding whether to trust
/// a low confidence.</param>
/// <param name="X">Where the monument block would stand, east–west.</param>
/// <param name="Y">Its height.</param>
/// <param name="Z">Its north–south position.</param>
/// <param name="Color">The wool colour the detector read it as, or absent where it could not tell.</param>
/// <param name="Confidence">How strongly, 0–1.</param>
/// <param name="Source">What found it — a sign, a pedestal, a shape.</param>
/// <param name="PedestalId">The block the monument stands on.</param>
/// <param name="PedestalData">That block's variant nibble.</param>
public sealed record MonumentSuggestionDto(
    int X, int Y, int Z,
    string? Color,
    double Confidence,
    string Source,
    [property: JsonPropertyName("pedestal_id")] int PedestalId,
    [property: JsonPropertyName("pedestal_data")] int PedestalData,
    SignPositionDto? Sign,
    string? Evidence);

/// <summary>Where a monument's naming sign stands.</summary>
/// <param name="X">Its block position, east–west.</param>
/// <param name="Y">Its height.</param>
/// <param name="Z">Its north–south position.</param>
public sealed record SignPositionDto(int X, int Y, int Z);

/// <summary>
/// The cores the scan found, and what a new one is made of if the author adds their own
/// (<c>GET /map/{slug}/core-suggestions</c>).
///
/// <para><see cref="Defaults"/> is answered beside the detections rather than restated in the client, so the
/// one definition of what a core is — <c>ObjectiveDefaults</c> — is not copied into the editor.</para>
/// </summary>
/// <param name="Defaults">What a core the author adds by hand would be.</param>
/// <param name="Cores">The casings the scan found, each read off the world rather than defaulted.</param>
public sealed record CoreSuggestionsDto(
    CoreDefaultsDto Defaults,
    IReadOnlyList<CoreSuggestionDto> Cores);

/// <summary>What a core is when nobody has said otherwise: how wide and tall its casing is, how thick the
/// shell, how far it floats and how far its lava may fall before the core counts as leaked.</summary>
/// <param name="Size">The casing's width and depth, in blocks.</param>
/// <param name="Height">Its height.</param>
/// <param name="Shell">How thick its wall is.</param>
/// <param name="Float">How far above the ground it stands. A core resting on the ground cannot leak.</param>
/// <param name="Leak">How far its lava must fall to count as leaked.</param>
public sealed record CoreDefaultsDto(int Size, int Height, int Shell, int Float, int Leak);

/// <summary>
/// One detected core casing. <see cref="Size"/> and <see cref="Height"/> are read off the box that was
/// actually found rather than taken from the defaults, so confirming a suggestion states the structure
/// standing in the world instead of the generator's idea of one.
/// </summary>
/// <param name="Lava">How many lava blocks the casing holds — the measure of whether it is a real core or a
/// shell someone never filled.</param>
/// <param name="Box">The block volume the casing occupies, for checking it in-game.</param>
/// <param name="Size">The casing's width and depth, read off the box that was found.</param>
/// <param name="Height">Its height, likewise read rather than defaulted.</param>
/// <param name="Shell">How thick its wall is.</param>
/// <param name="Float">How far above the ground it stands.</param>
/// <param name="OpenTop">Whether it is uncapped, its lava flush with the rim.</param>
public sealed record CoreSuggestionDto(
    CoreBoxDto Box,
    int Size,
    int Height,
    int Shell,
    int Float,
    bool OpenTop,
    int Lava);

/// <summary>A detected casing's block volume, inclusive on both ends.</summary>
/// <param name="MinX">Its west edge, inclusive.</param>
/// <param name="MinY">Its floor, inclusive.</param>
/// <param name="MinZ">Its north edge, inclusive.</param>
/// <param name="MaxX">Its east edge, inclusive.</param>
/// <param name="MaxY">Its ceiling, inclusive.</param>
/// <param name="MaxZ">Its south edge, inclusive.</param>
public sealed record CoreBoxDto(int MinX, int MinY, int MinZ, int MaxX, int MaxY, int MaxZ);
