using System.Text.Json;
using PgmStudio.Contracts;
using PgmStudio.Pgm;
using PgmStudio.Pgm.Editing;

namespace PgmStudio.Api.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// The records in <c>EditRequests</c> say what the sixteen edit routes take, and every one of those bodies is
/// read key by key by an editor in <c>Pgm/Editing</c> — one project below <c>Contracts</c>, so each route
/// declares its shape rather than binding it. Nothing in the compiler connects the two, so this does: a
/// populated record is serialized the way the wire carries it, parsed back into the dictionary the route
/// hands over, and given to the editor. A field the record spells differently from what the editor reads is
/// a key the editor never sees, and the assertion on what the edit did fails.
///
/// <para>The value is asymmetric and that is the point: an answer record that drifts is caught by
/// deserializing with unmapped members disallowed, but a request record that drifts is silent — the editor
/// takes its default and answers 200. So each field is asserted through its <b>effect</b>, not through the
/// round trip.</para>
/// </summary>
public sealed class EditRequestShapeTests
{
    /// <summary>The wire's own options — what FastEndpoints serializes a body with, so a record's published
    /// spelling is the one under test.</summary>
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    /// <summary>Put a request record through the path a posted body takes: serialize, then parse into the
    /// dictionary <c>WriteSupport.ReadPayloadAsync</c> hands the editor.</summary>
    private static Dict Posted<T>(T request) =>
        JsonTree.FromJson(JsonSerializer.Serialize(request, Wire)) as Dict ?? new Dict();

    private static Dict Map() => Serializer.ToDict(MapParser.ParseXmlString("""
        <?xml version="1.0"?>
        <map proto="1.4.0"><name>t</name><version>1</version><objective>o</objective>
        <teams><team id="red-team" color="red" max-players="20">Red</team></teams>
        <regions>
          <cuboid id="pad" min="0,0,0" max="4,4,4"/>
          <cuboid id="deck" min="6,0,6" max="9,4,9"/>
        </regions></map>
        """));

    // ── regions ─────────────────────────────────────────────────────────────────────

    /// <summary>Every geometry field of a create, asserted through the region it builds — one call per type,
    /// because a type reads only its own numbers and a field spelled wrong in the record is one the builder
    /// never receives.</summary>
    [Test]
    public async Task A_created_region_is_built_from_the_numbers_the_record_names()
    {
        var doc = Map();

        RegionEditor.CreateRegion(doc, Posted(new RegionCreateRequest(
            Type: "rectangle", Id: "yard", MinX: 10, MinZ: 20, MaxX: 30, MaxZ: 40)));
        await Assert.That(Bounds(doc, "yard")).IsEquivalentTo(new[] { 10.0, 20.0, 30.0, 40.0 });

        RegionEditor.CreateRegion(doc, Posted(new RegionCreateRequest(
            Type: "cuboid", Id: "hall", MinX: 1, MinY: 5, MinZ: 2, MaxX: 3, MaxY: 9, MaxZ: 4)));
        var hall = Region(doc, "hall");
        await Assert.That(Num(((Dict)hall["min"]!)["y"])).IsEqualTo(5.0);
        await Assert.That(Num(((Dict)hall["max"]!)["y"])).IsEqualTo(9.0);

        RegionEditor.CreateRegion(doc, Posted(new RegionCreateRequest(
            Type: "block", Id: "mark", X: 7, Y: 65, Z: 8)));
        var mark = (Dict)Region(doc, "mark")["position"]!;
        await Assert.That(Num(mark["x"])).IsEqualTo(7.0);
        await Assert.That(Num(mark["y"])).IsEqualTo(65.0);
        await Assert.That(Num(mark["z"])).IsEqualTo(8.0);

        RegionEditor.CreateRegion(doc, Posted(new RegionCreateRequest(
            Type: "cylinder", Id: "tower", BaseX: 50, BaseY: 62, BaseZ: 60, Radius: 4, Height: 12)));
        var tower = Region(doc, "tower");
        await Assert.That(Num(tower["radius"])).IsEqualTo(4.0);
        await Assert.That(Num(tower["height"])).IsEqualTo(12.0);
        await Assert.That(Num(((Dict)tower["base"]!)["y"])).IsEqualTo(62.0);
        await Assert.That(Bounds(doc, "tower")).IsEquivalentTo(new[] { 46.0, 56.0, 54.0, 64.0 });

        RegionEditor.CreateRegion(doc, Posted(new RegionCreateRequest(
            Type: "circle", Id: "ring", CenterX: 100, CenterZ: 200, Radius: 5)));
        await Assert.That(Bounds(doc, "ring")).IsEquivalentTo(new[] { 95.0, 195.0, 105.0, 205.0 });
    }

    /// <summary>The two fields of a create that are not geometry: the group it is filed under, and the draft
    /// mark the route itself reads off the same body.</summary>
    [Test]
    public async Task A_created_region_is_filed_under_the_category_the_record_names()
    {
        var doc = Map();
        var posted = Posted(new RegionCreateRequest(
            Type: "rectangle", Id: "yard", Category: "objective", DraftStep: "objective",
            MinX: 0, MinZ: 0, MaxX: 4, MaxZ: 4));

        RegionEditor.CreateRegion(doc, posted);

        var categories = (Dict)doc["region_categories"]!;
        await Assert.That((List<object?>)categories["objective"]!).Contains("yard");
        await Assert.That(posted.GetValueOrDefault("draft_step")).IsEqualTo("objective");
    }

    [Test]
    public async Task A_patched_region_moves_by_the_bounds_the_record_names()
    {
        var doc = Map();

        RegionEditor.PatchRegion(doc, "pad", Posted(new RegionPatchRequest(
            Bounds: new Bounds2dDto(MinX: 2, MinZ: 3, MaxX: 6, MaxZ: 7))));
        await Assert.That(Bounds(doc, "pad")).IsEquivalentTo(new[] { 2.0, 3.0, 6.0, 7.0 });

        // a cuboid takes its floor and ceiling under `coords`, and its footprint only under `bounds`
        RegionEditor.PatchRegion(doc, "pad", Posted(new RegionPatchRequest(
            Coords: new RegionCoordsDto(MinY: 11, MaxY: 22))));
        var pad = Region(doc, "pad");
        await Assert.That(Num(((Dict)pad["min"]!)["y"])).IsEqualTo(11.0);
        await Assert.That(Num(((Dict)pad["max"]!)["y"])).IsEqualTo(22.0);

        RegionEditor.PatchRegion(doc, "pad", Posted(new RegionPatchRequest(Id: "apron")));
        await Assert.That(Region(doc, "apron")["id"]).IsEqualTo("apron");
    }

    /// <summary>A cylinder and a circle read coords the cuboid does not, so the record's other half is
    /// asserted on the types that use it.</summary>
    [Test]
    public async Task A_patched_region_re_coordinates_by_the_type_the_record_names()
    {
        var doc = Map();
        RegionEditor.CreateRegion(doc, Posted(new RegionCreateRequest(
            Type: "cylinder", Id: "tower", BaseX: 0, BaseZ: 0, Radius: 1)));

        RegionEditor.PatchRegion(doc, "tower", Posted(new RegionPatchRequest(
            Coords: new RegionCoordsDto(BaseX: 20, BaseY: 70, BaseZ: 30, Radius: 5, Height: 9))));

        var tower = Region(doc, "tower");
        await Assert.That(Num(tower["height"])).IsEqualTo(9.0);
        await Assert.That(Num(((Dict)tower["base"]!)["y"])).IsEqualTo(70.0);
        await Assert.That(Bounds(doc, "tower")).IsEquivalentTo(new[] { 15.0, 25.0, 25.0, 35.0 });

        RegionEditor.CreateRegion(doc, Posted(new RegionCreateRequest(
            Type: "circle", Id: "ring", CenterX: 0, CenterZ: 0, Radius: 1)));
        RegionEditor.PatchRegion(doc, "ring", Posted(new RegionPatchRequest(
            Coords: new RegionCoordsDto(CenterX: 8, CenterZ: 9, Radius: 2))));
        await Assert.That(Bounds(doc, "ring")).IsEquivalentTo(new[] { 6.0, 7.0, 10.0, 11.0 });

        RegionEditor.CreateRegion(doc, Posted(new RegionCreateRequest(Type: "block", Id: "mark", X: 0, Z: 0)));
        RegionEditor.PatchRegion(doc, "mark", Posted(new RegionPatchRequest(
            Coords: new RegionCoordsDto(X: 4, Y: 5, Z: 6))));
        var mark = (Dict)Region(doc, "mark")["position"]!;
        await Assert.That(Num(mark["x"])).IsEqualTo(4.0);
        await Assert.That(Num(mark["z"])).IsEqualTo(6.0);
    }

    [Test]
    public async Task A_group_takes_the_children_and_the_compound_type_the_record_names()
    {
        var doc = Map();
        var grouped = RegionEditor.GroupRegions(doc, Posted(new RegionGroupRequest(
            ChildIds: ["pad", "deck"], Type: "intersect", Id: "overlap")));

        await Assert.That(grouped["id"]).IsEqualTo("overlap");
        await Assert.That(Region(doc, "overlap")["type"]).IsEqualTo("intersect");

        var freed = RegionEditor.UngroupRegion(doc, Posted(new RegionUngroupRequest(RegionId: "overlap")));
        await Assert.That((List<object?>)freed["child_ids"]!).IsEquivalentTo(new object?[] { "pad", "deck" });
    }

    // ── spawns ──────────────────────────────────────────────────────────────────────

    [Test]
    public async Task A_spawn_takes_the_region_team_kit_and_yaw_the_record_names()
    {
        var doc = Map();
        SpawnEditor.AddSpawnLink(doc, Posted(new SpawnCreateRequest(
            RegionId: "pad", Team: "red-team", Kit: "spawn-kit", Yaw: 90)));

        var spawn = Spawn(doc, "pad");
        await Assert.That(spawn["team"]).IsEqualTo("red-team");
        await Assert.That(spawn["kit"]).IsEqualTo("spawn-kit");
        await Assert.That(Num(spawn["yaw"])).IsEqualTo(90.0);

        SpawnEditor.UpdateSpawnLink(doc, "pad", Posted(new SpawnUpdateRequest(Kit: "rush", Yaw: 180)));
        await Assert.That(Spawn(doc, "pad")["kit"]).IsEqualTo("rush");
        await Assert.That(Num(Spawn(doc, "pad")["yaw"])).IsEqualTo(180.0);
    }

    [Test]
    public async Task An_observer_spawn_takes_the_region_kit_and_yaw_the_record_names()
    {
        var doc = Map();
        SpawnEditor.SetObserverSpawn(doc, Posted(new ObserverSpawnRequest(
            RegionId: "deck", Kit: "obs", Yaw: 45)));

        var observer = (Dict)doc["observer_spawn"]!;
        await Assert.That(observer["region"]).IsEqualTo("deck");
        await Assert.That(observer["kit"]).IsEqualTo("obs");
        await Assert.That(Num(observer["yaw"])).IsEqualTo(45.0);
    }

    // ── teams ───────────────────────────────────────────────────────────────────────

    [Test]
    public async Task A_team_takes_every_field_the_record_names()
    {
        var doc = Map();
        var made = (Dict)TeamEditor.AddTeam(doc, Posted(new TeamCreateRequest(
            Id: "blue", Name: "Blue", Color: "blue", DyeColor: "light blue",
            MaxPlayers: 24, MinPlayers: 2)))["team"]!;

        await Assert.That(made["name"]).IsEqualTo("Blue");
        await Assert.That(made["color"]).IsEqualTo("blue");
        await Assert.That(made["dye_color"]).IsEqualTo("light blue");
        await Assert.That(Convert.ToInt32(made["max_players"])).IsEqualTo(24);
        await Assert.That(Convert.ToInt32(made["min_players"])).IsEqualTo(2);

        var changed = (Dict)TeamEditor.UpdateTeam(doc, "blue", Posted(new TeamUpdateRequest(
            Id: "azure", Name: "Azure", Color: "aqua", DyeColor: "cyan",
            MaxPlayers: 12, MinPlayers: 1)))["team"]!;

        await Assert.That(changed["id"]).IsEqualTo("azure");
        await Assert.That(changed["color"]).IsEqualTo("aqua");
        await Assert.That(changed["dye_color"]).IsEqualTo("cyan");
        await Assert.That(Convert.ToInt32(changed["max_players"])).IsEqualTo(12);
        await Assert.That(Convert.ToInt32(changed["min_players"])).IsEqualTo(1);
    }

    // ── wools and monuments ─────────────────────────────────────────────────────────

    [Test]
    public async Task A_wool_takes_the_colour_team_location_and_room_the_record_names()
    {
        var doc = Map();
        WoolEditor.AddWool(doc, Posted(new WoolCreateRequest(Color: "light_blue")));
        await Assert.That(Wool(doc, "light_blue")["color"]).IsEqualTo("light_blue");

        WoolEditor.UpdateWool(doc, "light_blue", Posted(new WoolUpdateRequest(
            Color: "cyan", Team: "red-team", Location: new LocationDto(11, 64, 12), WoolRoomRegion: "pad")));

        var wool = Wool(doc, "cyan");
        await Assert.That(wool["team"]).IsEqualTo("red-team");
        await Assert.That(wool["wool_room_region"]).IsEqualTo("pad");
        await Assert.That(Xyz(wool["location"])).IsEquivalentTo(new[] { 11.0, 64.0, 12.0 });
    }

    /// <summary>One record adds a monument and changes one, so both readings of it are exercised.</summary>
    [Test]
    public async Task A_monument_takes_the_team_location_and_region_the_record_names()
    {
        var doc = Map();
        WoolEditor.AddWool(doc, Posted(new WoolCreateRequest(Color: "red")));

        var made = (Dict)WoolEditor.AddMonument(doc, "red", Posted(new MonumentWriteRequest(
            Team: "red-team", Location: new LocationDto(1, 2, 3), MonumentRegion: "pad")))["monument"]!;
        await Assert.That(made["team"]).IsEqualTo("red-team");
        await Assert.That(made["monument_region"]).IsEqualTo("pad");
        await Assert.That(Xyz(made["location"])).IsEquivalentTo(new[] { 1.0, 2.0, 3.0 });

        var changed = (Dict)WoolEditor.UpdateMonument(doc, "red", (string)made["id"]!,
            Posted(new MonumentWriteRequest(Location: new LocationDto(4, 5, 6), MonumentRegion: "deck")))["monument"]!;
        await Assert.That(changed["monument_region"]).IsEqualTo("deck");
        await Assert.That(Xyz(changed["location"])).IsEquivalentTo(new[] { 4.0, 5.0, 6.0 });
    }

    // ── metadata ────────────────────────────────────────────────────────────────────

    /// <summary>Metadata has no editor — the endpoint reads the same dictionary straight into an update — so
    /// the record is held to the keys that endpoint asks for by name.</summary>
    [Test]
    public async Task The_metadata_record_carries_the_keys_the_columns_are_set_from()
    {
        var posted = Posted(new MapMetadataRequest(
            Name: "Sentient", Version: "1.2", Objective: "Capture the wool",
            MaxBuildHeight: 96, Authors: [new MapAuthorDto("uuid-1", "author", "layout", "Rocky")]));

        await Assert.That(posted["name"]).IsEqualTo("Sentient");
        await Assert.That(posted["version"]).IsEqualTo("1.2");
        await Assert.That(posted["objective"]).IsEqualTo("Capture the wool");
        await Assert.That(Num(posted["max_build_height"])).IsEqualTo(96.0);

        var author = (Dict)((List<object?>)posted["authors"]!)[0]!;
        await Assert.That(author["uuid"]).IsEqualTo("uuid-1");
        await Assert.That(author["role"]).IsEqualTo("author");
        await Assert.That(author["contribution"]).IsEqualTo("layout");
        await Assert.That(author["name"]).IsEqualTo("Rocky");
    }

    // ── reading the document under test ─────────────────────────────────────────────

    private static Dict Region(Dict doc, string id) => (Dict)((Dict)doc["regions"]!)[id]!;

    private static double[] Bounds(Dict doc, string id)
    {
        var box = (Dict)Region(doc, id)["bounds_2d"]!;
        var min = (Dict)box["min"]!;
        var max = (Dict)box["max"]!;
        return [Num(min["x"]), Num(min["z"]), Num(max["x"]), Num(max["z"])];
    }

    private static Dict Spawn(Dict doc, string regionId) =>
        ((List<object?>)doc["spawns"]!).OfType<Dict>().First(s => s.GetValueOrDefault("region") as string == regionId);

    private static Dict Wool(Dict doc, string id) =>
        ((List<object?>)doc["wools"]!).OfType<Dict>().First(w => w.GetValueOrDefault("id") as string == id);

    private static double[] Xyz(object? location)
    {
        var point = (Dict)location!;
        return [Num(point["x"]), Num(point["y"]), Num(point["z"])];
    }

    private static double Num(object? value) => Convert.ToDouble(value);
}
