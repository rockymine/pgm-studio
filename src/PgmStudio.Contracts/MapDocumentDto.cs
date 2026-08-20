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
public sealed record MapDocumentDto(
    string? Name,
    string? Version,
    IReadOnlyList<string> Gamemode,
    string? Objective,
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
public sealed record MapAuthorDto(string Uuid, string? Role, string? Contribution, string? Name);

/// <summary>One team, as the contract states it.</summary>
public sealed record MapTeamDto(
    string Id, string? Name, string? Color,
    [property: JsonPropertyName("dye_color")] string? DyeColor,
    [property: JsonPropertyName("max_players")] int? MaxPlayers,
    [property: JsonPropertyName("min_players")] int? MinPlayers);

/// <summary>Where a team enters. <paramref name="Region"/> is a region id where the spawn names one and a
/// whole inline region where it states one, which is the contract's own choice and stays open here.</summary>
public sealed record MapSpawnDto(string? Team, string? Kit, double? Yaw, JsonElement Region);

/// <summary>One wool objective and the monuments it is placed on, grouped the way the map document carries
/// them rather than the way the rows store them.</summary>
public sealed record MapWoolDto(
    string Id, string? Color, string? Team,
    JsonElement Location,
    [property: JsonPropertyName("wool_room_region")] string? WoolRoomRegion,
    IReadOnlyList<MapMonumentDto> Monuments);

/// <summary>One monument a wool is placed on.</summary>
public sealed record MapMonumentDto(
    string Id, string? Team,
    JsonElement Location,
    [property: JsonPropertyName("monument_region")] string? MonumentRegion);
