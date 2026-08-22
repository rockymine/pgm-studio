using System.Text.Json;
using System.Text.Json.Serialization;
using PgmStudio.Contracts;
using PgmStudio.Pgm;
using PgmStudio.Pgm.Editing;

namespace PgmStudio.Api.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// The records in <c>EditDtos</c> say what the twenty-four edit routes answer, and every one of those answers
/// is built by an editor in <c>Pgm/Editing</c> — one project below <c>Contracts</c>, so each route declares
/// its shape rather than mapping it. Nothing in the compiler connects the two, so this does: the editor's
/// real return value is deserialized into the record with unmapped members <b>disallowed</b>, which fails the
/// moment an editor writes a key the record — and so the published schema — has no field for.
///
/// <para>The optional halves are exercised on both sides: a rename that moves no footprint against one that
/// does, an ordered compound that warns on dissolve against a union that does not, a single counterpart
/// against a whole orbit.</para>
/// </summary>
public sealed class EditAnswerShapeTests
{
    private static readonly JsonSerializerOptions Strict = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private static Dict Map() => Serializer.ToDict(MapParser.ParseXmlString("""
        <?xml version="1.0"?>
        <map proto="1.4.0"><name>t</name><version>1</version><objective>o</objective>
        <teams><team id="red-team" color="red" max-players="20">Red</team></teams>
        <regions>
          <cuboid id="pad" min="0,0,0" max="4,4,4"/>
          <cuboid id="deck" min="6,0,6" max="9,4,9"/>
          <cuboid id="hole" min="1,0,1" max="2,4,2"/>
        </regions></map>
        """));

    /// <summary>Read an editor's answer back through the record the route publishes.</summary>
    private static T Answer<T>(Dict written) =>
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(written, Strict), Strict)!;

    // ── the empty answer, which eleven routes give ──────────────────────────────────

    [Test]
    public async Task An_edit_with_nothing_to_hand_back_answers_the_empty_object()
    {
        var doc = Map();
        SpawnEditor.AddSpawnLink(doc, new Dict { ["region_id"] = "pad", ["team"] = "red-team" });

        await Assert.That(Answer<AppliedDto>(SpawnEditor.DeleteSpawnLink(doc, "pad"))).IsNotNull();
        await Assert.That(Answer<AppliedDto>(RegionEditor.DeleteRegion(doc, "deck"))).IsNotNull();
        await Assert.That(Answer<AppliedDto>(TeamEditor.DeleteTeam(doc, "red-team"))).IsNotNull();
        // and it really is `{}` on the wire, not a field the caller has to ignore
        await Assert.That(JsonSerializer.Serialize(new AppliedDto(), Strict)).IsEqualTo("{}");
    }

    // ── regions ─────────────────────────────────────────────────────────────────────

    [Test]
    public async Task A_created_region_answers_the_id_it_is_named_by()
    {
        var made = RegionEditor.CreateRegion(Map(),
            new Dict { ["type"] = "rectangle", ["coords"] = new Dict { ["min_x"] = 0L, ["min_z"] = 0L, ["max_x"] = 8L, ["max_z"] = 8L } });

        await Assert.That(Answer<RegionCreatedDto>(made).Id).IsEqualTo("region_1");
    }

    /// <summary>A move answers the footprint it moved to; a rename answers no bounds at all, which is why the
    /// field is optional rather than nulled.</summary>
    [Test]
    public async Task A_patched_region_answers_bounds_only_where_it_moved()
    {
        var doc = Map();
        var moved = Answer<RegionPatchedDto>(RegionEditor.PatchRegion(doc, "pad",
            new Dict { ["coords"] = new Dict { ["min_x"] = 2L, ["min_z"] = 0L, ["max_x"] = 6L, ["max_z"] = 4L } }));
        await Assert.That(moved.Bounds).IsNotNull();
        await Assert.That(moved.Bounds!.MaxX).IsEqualTo(6.0);

        var renamed = Answer<RegionPatchedDto>(RegionEditor.PatchRegion(doc, "pad", new Dict { ["id"] = "apron" }));
        await Assert.That(renamed.Bounds).IsNull();
    }

    [Test]
    public async Task A_grouped_region_answers_the_compound_and_what_it_covers()
    {
        var grouped = Answer<RegionGroupedDto>(RegionEditor.GroupRegions(Map(),
            new Dict { ["type"] = "union", ["child_ids"] = new List<object?> { "pad", "deck" } }));

        await Assert.That(grouped.Id).IsEqualTo("union_1");
        await Assert.That(grouped.Bounds.MaxX).IsEqualTo(9.0);
    }

    /// <summary>Dissolving frees the children; an ordered compound also loses something its children cannot
    /// carry, and says so.</summary>
    [Test]
    public async Task An_ungrouped_region_answers_the_children_and_warns_only_where_order_was_lost()
    {
        var plain = Map();
        RegionEditor.GroupRegions(plain, new Dict { ["type"] = "union", ["child_ids"] = new List<object?> { "pad", "deck" } });
        var freed = Answer<RegionUngroupedDto>(RegionEditor.UngroupRegion(plain, new Dict { ["region_id"] = "union_1" }));
        await Assert.That(freed.ChildIds).IsEquivalentTo(new[] { "pad", "deck" });
        await Assert.That(freed.Warning).IsNull();

        var ordered = Map();
        RegionEditor.GroupRegions(ordered, new Dict { ["type"] = "negative", ["child_ids"] = new List<object?> { "pad", "hole" } });
        var dissolved = Answer<RegionUngroupedDto>(RegionEditor.UngroupRegion(ordered, new Dict { ["region_id"] = "negative_1" }));
        await Assert.That(dissolved.Warning).IsNotNull();
    }

    /// <summary>One counterpart names its opposite; a whole orbit names only what it made.</summary>
    [Test]
    public async Task A_fanned_region_answers_what_it_created()
    {
        var one = Answer<RegionOrbitDto>(SymmetryAuthoring.CreateCounterpart(Map(), "pad", "mirror_x", 0, 0));
        await Assert.That(one.Counterpart).IsNotNull();
        await Assert.That(one.Created).IsNotEmpty();

        var orbit = Answer<RegionOrbitDto>(SymmetryAuthoring.CreateOrbit(Map(), "pad", "rot_90", 0, 0));
        await Assert.That(orbit.Counterpart).IsNull();
        await Assert.That(orbit.Created.Count).IsEqualTo(3);
    }

    // ── the three rows that answer themselves ───────────────────────────────────────

    [Test]
    public async Task A_written_wool_answers_the_wool_the_map_document_carries()
    {
        var doc = Map();
        var made = Answer<WoolWrittenDto>(WoolEditor.AddWool(doc, new Dict { ["color"] = "red" }));
        await Assert.That(made.Wool.Color).IsEqualTo("red");
        await Assert.That(made.Wool.Monuments).IsEmpty();

        var withRoom = Answer<WoolWrittenDto>(WoolEditor.UpdateWool(doc, "red",
            new Dict { ["wool_room_region"] = "pad", ["team"] = "red-team" }));
        await Assert.That(withRoom.Wool.WoolRoomRegion).IsEqualTo("pad");
    }

    [Test]
    public async Task A_written_monument_answers_the_monument_the_map_document_carries()
    {
        var doc = Map();
        WoolEditor.AddWool(doc, new Dict { ["color"] = "red" });
        var made = Answer<MonumentWrittenDto>(WoolEditor.AddMonument(doc, "red",
            new Dict { ["team"] = "red-team", ["monument_region"] = "pad" }));

        await Assert.That(made.Monument.Team).IsEqualTo("red-team");
        await Assert.That(made.Monument.MonumentRegion).IsEqualTo("pad");
    }

    /// <summary>A created team carries no dye colour and an updated one may, so the record has to take both.</summary>
    [Test]
    public async Task A_written_team_answers_the_team_the_map_document_carries()
    {
        var doc = Map();
        var made = Answer<TeamWrittenDto>(TeamEditor.AddTeam(doc, new Dict { ["id"] = "blue", ["name"] = "Blue" }));
        await Assert.That(made.Team.Id).IsEqualTo("blue");
        await Assert.That(made.Team.DyeColor).IsNull();

        var dyed = Answer<TeamWrittenDto>(TeamEditor.UpdateTeam(doc, "blue", new Dict { ["dye_color"] = "light blue" }));
        await Assert.That(dyed.Team.DyeColor).IsEqualTo("light blue");
    }
}
