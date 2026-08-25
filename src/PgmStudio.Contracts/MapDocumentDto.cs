using System.Text.Json;
using System.Text.Json.Serialization;

namespace PgmStudio.Contracts;

/// <summary>
/// A stored map, whole (<c>GET /map/{slug}</c>) — the map.xml contract reconstructed from the relational
/// rows, in the same JSON shape the importer read it from.
///
/// <para>Named rather than mapped: the encoding below the top level is the codec's, conditional field by
/// field (an author's contribution appears only when there is one, a spawn's region is a key or a whole
/// region), and a second walk of it here would be a second codec free to disagree with the first. So the
/// keys a caller reads by name are typed and the codec's own encodings stay open. <c>MapDocumentShapeTests</c>
/// holds this record to what the serializer writes.</para>
/// </summary>
/// <param name="Gamemode">What the author declared, which is often nothing.</param>
/// <param name="Gamemodes">What the map's modules make it — the derived truth beside the declared label.
/// Absent on a map whose modules imply none.</param>
/// <param name="Filters">Filter definitions by id; the encoding is the contract's and open by design.</param>
/// <param name="Regions">Region definitions by id, same.</param>
/// <param name="Name">The map's title.</param>
/// <param name="Version">The version its <c>map.xml</c> states.</param>
/// <param name="Created">When the map was made, as <c>yyyy-mm-dd</c>. Empty where the map does not say —
/// the studio takes this from whoever authors the map and derives nothing.</param>
/// <param name="Phase">How finished the map is: <c>development</c> on every map the studio authors, and
/// whatever a parsed map states. Empty where the map does not say.</param>
/// <param name="Objective">The one-line objective sentence a player is shown.</param>
/// <param name="MaxBuildHeight">The ceiling players may build to, where the map states one.</param>
/// <param name="Authors">Who is credited, and for what.</param>
/// <param name="Kits">Kit definitions, in the contract's own encoding.</param>
/// <param name="Teams">The teams that play it.</param>
/// <param name="Spawns">Where each team enters.</param>
/// <param name="ObserverSpawn">Where observers watch from, where the map states one.</param>
/// <param name="Wools">The wool objectives, each with the monuments it is placed on.</param>
/// <param name="Spawners">Dispenser and spawner definitions, in the contract's own encoding.</param>
/// <param name="Renewables">The regions whose blocks grow back.</param>
/// <param name="BlockDropRules">What breaking a block yields, where the map overrides it.</param>
/// <param name="ApplyRules">Which filter applies where — the wiring between regions and filters.</param>
/// <param name="Destroyables">The DTM goals, absent on a map with none.</param>
/// <param name="Cores">The DTC goals, absent on a map with none.</param>
/// <param name="Modes">The timed mode changes, absent on a map with none.</param>
public sealed record MapDocumentDto(
    string? Name,
    string? Version,
    IReadOnlyList<string> Gamemode,
    string? Objective,
    string? Created,
    string? Phase,
    [property: JsonPropertyName("max_build_height")] int? MaxBuildHeight,
    IReadOnlyList<MapAuthorDto> Authors,
    IReadOnlyList<JsonElement> Kits,
    IReadOnlyList<MapTeamDto> Teams,
    IReadOnlyList<MapSpawnDto> Spawns,
    [property: JsonPropertyName("observer_spawn")] MapSpawnDto? ObserverSpawn,
    IReadOnlyList<MapWoolDto> Wools,
    IReadOnlyList<JsonElement> Spawners,
    IReadOnlyList<JsonElement> Renewables,
    [property: JsonPropertyName("block_drop_rules")] IReadOnlyList<JsonElement> BlockDropRules,
    IReadOnlyDictionary<string, JsonElement> Filters,
    IReadOnlyDictionary<string, JsonElement> Regions,
    [property: JsonPropertyName("apply_rules")] IReadOnlyList<JsonElement> ApplyRules,
    IReadOnlyList<JsonElement>? Destroyables = null,
    IReadOnlyList<JsonElement>? Cores = null,
    IReadOnlyList<JsonElement>? Modes = null,
    IReadOnlyList<string>? Gamemodes = null);

/// <summary>One credited author. <paramref name="Name"/> is a studio-side display cache rather than part of
/// the contract, so it is present only where the studio has one.</summary>
/// <param name="Uuid">The account credited, which is what the contract stores.</param>
/// <param name="Role">What they are credited as, where the map says.</param>
/// <param name="Contribution">What they contributed, where the map says.</param>
/// <param name="Name">Their username, as a studio-side display cache. It is not part of the contract, so it
/// is present only where the studio has resolved one.</param>
public sealed record MapAuthorDto(string Uuid, string? Role, string? Contribution, string? Name);

/// <summary>One team, as the contract states it.</summary>
/// <param name="Id">What the rest of the document names the team by.</param>
/// <param name="Name">The team as a player reads it.</param>
/// <param name="Color">The colour it plays in.</param>
/// <param name="DyeColor">The dye its wools take, where that differs from the team colour.</param>
/// <param name="MaxPlayers">How many may join it.</param>
/// <param name="MinPlayers">How many are needed before the match starts.</param>
public sealed record MapTeamDto(
    string Id, string? Name, string? Color,
    [property: JsonPropertyName("dye_color")] string? DyeColor,
    [property: JsonPropertyName("max_players")] int? MaxPlayers,
    [property: JsonPropertyName("min_players")] int? MinPlayers);

/// <summary>Where a team enters. <paramref name="Region"/> is a region id where the spawn names one and a
/// whole inline region where it states one, which is the contract's own choice and stays open here.</summary>
/// <param name="Team">The team that enters here, or absent for the observer spawn.</param>
/// <param name="Kit">The kit given on entering.</param>
/// <param name="Yaw">Which way the player faces, in degrees.</param>
/// <param name="Region">Where they arrive: a region id where the spawn names one, a whole inline region
/// where it states one — the contract's own choice, kept open here.</param>
public sealed record MapSpawnDto(string? Team, string? Kit, double? Yaw, JsonElement Region);

/// <summary>One wool objective and the monuments it is placed on, grouped the way the map document carries
/// them rather than the way the rows store them.</summary>
/// <param name="Id">What the rest of the document names the objective by.</param>
/// <param name="Color">The wool colour to be captured.</param>
/// <param name="Team">The team that must capture it.</param>
/// <param name="Location">Where the wool is placed, in the contract's own encoding.</param>
/// <param name="WoolRoomRegion">The region holding the room the wool is taken from, where the map names
/// one.</param>
/// <param name="Monuments">The places it may be placed to score.</param>
public sealed record MapWoolDto(
    string Id, string? Color, string? Team,
    JsonElement Location,
    [property: JsonPropertyName("wool_room_region")] string? WoolRoomRegion,
    IReadOnlyList<MapMonumentDto> Monuments);

/// <summary>One monument a wool is placed on.</summary>
/// <param name="Id">What the document names the monument by.</param>
/// <param name="Team">The team that defends it.</param>
/// <param name="Location">The block the wool is placed on, in the contract's own encoding.</param>
/// <param name="MonumentRegion">The region the monument stands in, where the map names one.</param>
public sealed record MapMonumentDto(
    string Id, string? Team,
    JsonElement Location,
    [property: JsonPropertyName("monument_region")] string? MonumentRegion);
