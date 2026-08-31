using System.Text.Json;
using PgmStudio.Geom;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// The authored box annotation: the wire shape (round-trip, kind canonicalisation), the two membership modes
/// (named members vs containment), and the invariant that a box changes nothing downstream — a plan compiles
/// and evaluates identically with or without its boxes drawn.
/// </summary>
public sealed class PlanBoxesTests
{
    private const string Json = """
    {
      "plan": 2,
      "meta": { "name": "T" },
      "globals": { "cell": 5, "symmetry": "rot_180", "maxPlayers": 12, "surface": 9, "headroom": 11 },
      "pieces": [
        { "id": "entry", "role": "piece",     "rect": [0, 0, 2, 1] },
        { "id": "room",  "role": "wool-room", "rect": [2, 0, 1, 1] },
        { "id": "far",   "role": "piece",     "rect": [8, 8, 1, 1] },
        { "id": "gap",   "role": "buffer",    "rect": [0, 1, 3, 1] }
      ],
      "zones": [],
      "placements": { "spawns": [], "wools": [], "iron": [] },
      "boxes": [
        { "id": "wool-a", "kind": "wool", "rect": [0, 0, 3, 2] },
        { "id": "named",  "kind": "hub",  "rect": [0, 0, 1, 1], "members": ["far", "room"] }
      ]
    }
    """;

    [Test]
    public async Task Parses_boxes_and_round_trips_them()
    {
        var plan = PlanModel.Parse(Json)!;
        await Assert.That(plan.Boxes.Count).IsEqualTo(2);
        await Assert.That(plan.Boxes[0].Kind).IsEqualTo(PlanBoxKinds.Wool);
        await Assert.That(plan.Boxes[0].Rect).IsEqualTo(new CellRect(0, 0, 3, 2));
        await Assert.That(plan.Boxes[1].Members!).IsEquivalentTo(new[] { "far", "room" });

        var json = plan.ToJson();
        // A typed rect still writes the four-element array the format has always stored (whitespace is the
        // serializer's business — what matters is that it is an array of four numbers, not an object).
        var wire = System.Text.Json.JsonDocument.Parse(json)
            .RootElement.GetProperty("boxes")[0].GetProperty("rect");
        await Assert.That(wire.ValueKind).IsEqualTo(System.Text.Json.JsonValueKind.Array)
            .Because("a CellRect keeps the plan.json wire form");
        await Assert.That(wire.EnumerateArray().Select(e => e.GetInt32()).ToArray())
            .IsEquivalentTo(new[] { 0, 0, 3, 2 });

        var reparsed = PlanModel.Parse(json)!;
        await Assert.That(reparsed.Boxes.Count).IsEqualTo(2);
        await Assert.That(reparsed.Boxes[0].Rect).IsEqualTo(new CellRect(0, 0, 3, 2));
        await Assert.That(reparsed.Boxes[1].Members!.Count).IsEqualTo(2);
    }

    [Test]
    public async Task An_unknown_kind_loads_as_the_unclassified_mid()
    {
        var plan = PlanModel.Parse("""
        { "plan": 2, "globals": {}, "pieces": [], "boxes": [ { "id": "b", "kind": "nonsense", "rect": [0,0,1,1] } ] }
        """)!;
        await Assert.That(plan.Boxes[0].Kind).IsEqualTo(PlanBoxKinds.Mid);
    }

    [Test]
    public async Task A_plan_without_boxes_has_an_empty_list_not_null()
    {
        var plan = PlanModel.Parse("""{ "plan": 2, "globals": {}, "pieces": [] }""")!;
        await Assert.That(plan.Boxes).IsEmpty();
    }

    [Test]
    public async Task Containment_membership_takes_the_generating_pieces_wholly_inside()
    {
        var plan = PlanModel.Parse(Json)!;
        var members = PlanBoxes.MembersOf(plan, plan.Boxes[0]).Select(p => p.Id).ToList();
        // `entry` + `room` are inside; `far` is outside; `gap` is inside but an annotation, never a member.
        await Assert.That(members).IsEquivalentTo(new[] { "entry", "room" });
    }

    [Test]
    public async Task Named_members_win_over_containment_and_ignore_the_rect()
    {
        var plan = PlanModel.Parse(Json)!;
        // The rect covers only `entry`'s first cell, but the named list is the answer.
        var members = PlanBoxes.MembersOf(plan, plan.Boxes[1]).Select(p => p.Id).ToList();
        await Assert.That(members).IsEquivalentTo(new[] { "room", "far" });
    }

    [Test]
    public async Task A_named_member_that_no_longer_exists_is_skipped()
    {
        var plan = PlanModel.Parse(Json)!;
        plan.Boxes[1].Members = ["room", "deleted"];
        var members = PlanBoxes.MembersOf(plan, plan.Boxes[1]).Select(p => p.Id).ToList();
        await Assert.That(members).IsEquivalentTo(new[] { "room" });
    }

    [Test]
    public async Task Contains_counts_touching_edges_as_inside()
    {
        await Assert.That(PlanBoxes.Contains(new(0, 0, 2, 2), new(0, 0, 2, 2))).IsTrue();     // exactly filling
        await Assert.That(PlanBoxes.Contains(new(0, 0, 2, 2), new(1, 1, 1, 1))).IsTrue();
        await Assert.That(PlanBoxes.Contains(new(0, 0, 2, 2), new(1, 1, 2, 1))).IsFalse();    // overhangs
        await Assert.That(PlanBoxes.Contains(new(0, 0, 2, 2), new(-1, 0, 1, 1))).IsFalse();
    }

    [Test]
    public async Task Boxes_are_authoring_only_the_compiled_output_ignores_them()
    {
        var withBoxes = PlanModel.Parse(
            PlanTestSupport.ReadSeed("shifted-u-frontline-attach-hole-hub.plan.json"))!;
        await Assert.That(withBoxes.Boxes).IsNotEmpty();

        var without = PlanModel.Parse(withBoxes.ToJson())!;
        without.Boxes = [];

        var (layoutA, intentA) = PlanCompiler.Compile(withBoxes);
        var (layoutB, intentB) = PlanCompiler.Compile(without);
        var opts = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        await Assert.That(JsonSerializer.Serialize(layoutA, opts)).IsEqualTo(JsonSerializer.Serialize(layoutB, opts));
        await Assert.That(JsonSerializer.Serialize(intentA, opts)).IsEqualTo(JsonSerializer.Serialize(intentB, opts));

        var findingsA = PlanValidator.Check(withBoxes).Select(f => f.Message).ToList();
        var findingsB = PlanValidator.Check(without).Select(f => f.Message).ToList();
        await Assert.That(findingsA).IsEquivalentTo(findingsB);
    }
}
