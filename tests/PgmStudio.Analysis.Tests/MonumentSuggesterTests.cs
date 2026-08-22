using PgmStudio.Analysis.Suggest;
using PgmStudio.Domain;
using PgmStudio.Geom;

namespace PgmStudio.Analysis.Tests;

/// <summary>
/// The authoring-flow monument suggester: the text classifier, and the derivation over a world that has
/// already been read.
///
/// <para>The fixtures are <see cref="WorldReading"/>s built by hand — a block at a position, a sign with its
/// text, a stand with its name — rather than Anvil chunks with NBT inside them. That is the point of the
/// boundary: what a monument <em>is</em> can be stated without a world file, and a test that had to build one
/// was a test of the decoder as much as of the derivation. How a sign's four lines become a sentence and
/// which NBT list holds a 1.8 head are <c>WorldReader</c>'s, and are tested there.</para>
///
/// <para>Corpus precision/recall (thunder/pigland/dragons_hearth → 100% with the style declared) is covered
/// by the RoundTrip <c>--suggest-monuments</c> harness.</para>
/// </summary>
public class MonumentSuggesterTests
{
    private static readonly BlockBox Whole = new(0, 0, 0, 15, 15, 15);

    /// <summary>A world holding the blocks given, and whatever markers a test adds to it.</summary>
    private static WorldReading Read(
        (int X, int Y, int Z, int Id, int Data)[] blocks,
        IReadOnlyList<ReadSign>? signs = null,
        IReadOnlyList<ReadStand>? stands = null,
        IReadOnlyList<ReadFrame>? frames = null) =>
        new(blocks.ToDictionary(b => (b.X, b.Y, b.Z), b => (b.Id, b.Data)),
            signs ?? [], stands ?? [], frames ?? []);

    // ---- text classifier (pure) ----

    [Test]
    public async Task IsMonumentLabel_accepts_real_labels()
    {
        await Assert.That(MonumentSuggester.IsMonumentLabel("Place GREEN WOOL here!")).IsTrue();
        await Assert.That(MonumentSuggester.IsMonumentLabel("Green Wool\nmonument")).IsTrue();
        await Assert.That(MonumentSuggester.IsMonumentLabel("-------> Red Wool ------->")).IsTrue();   // decoration stripped before length gate
    }

    [Test]
    public async Task IsMonumentLabel_rejects_the_false_positive_signage()
    {
        await Assert.That(MonumentSuggester.IsMonumentLabel("RED TEAM ONLY")).IsFalse();
        await Assert.That(MonumentSuggester.IsMonumentLabel("Kill sheep to get wool!")).IsFalse();
        await Assert.That(MonumentSuggester.IsMonumentLabel("Victory Monument")).IsFalse();
        await Assert.That(MonumentSuggester.IsMonumentLabel("Back to the woolroom")).IsFalse();
        await Assert.That(MonumentSuggester.IsMonumentLabel("v v v v")).IsFalse();   // bare arrows
    }

    [Test]
    public async Task ColorFromText_prefers_the_longest_match()
    {
        await Assert.That(MonumentSuggester.ColorFromText("Place Light Blue Wool here")).IsEqualTo("light_blue");
        await Assert.That(MonumentSuggester.ColorFromText("Green Wool")).IsEqualTo("green");
        await Assert.That(MonumentSuggester.ColorFromText("nothing here")).IsNull();
    }

    // ---- geometry: wall sign on the block below the monument ----

    /// <summary>A bedrock pedestal at (5,7,5) with the monument's air cell above it, and a wall sign beside
    /// the pedestal naming the colour.</summary>
    private static WorldReading SignBelow() =>
        Read([(5, 7, 5, 7, 0), (5, 7, 6, 68, 3)], signs: [new ReadSign(5, 7, 6, "Green Wool")]);

    [Test]
    public async Task SignBelow_predicts_the_air_cell_above_the_pedestal()
    {
        var found = MonumentSuggester.Suggest(SignBelow(), Whole,
            new MonumentStyle(PedestalKind.Bedrock, LabelKind.SignBelow)).Single();
        await Assert.That((found.X, found.Y, found.Z)).IsEqualTo((5, 8, 5));
        await Assert.That(found.Color).IsEqualTo("green");
        await Assert.That(found.Source).IsEqualTo("sign");
        await Assert.That(found.PedestalId).IsEqualTo(7);
    }

    [Test]
    public async Task Declared_pedestal_filters_out_the_wrong_style()
    {
        var none = MonumentSuggester.Suggest(SignBelow(), Whole,
            new MonumentStyle(PedestalKind.StainedGlass, LabelKind.SignBelow));
        await Assert.That(none).IsEmpty();   // pedestal is bedrock, not glass
    }

    [Test]
    public async Task Gather_is_style_agnostic_and_Score_applies_the_style(/* F9 */)
    {
        // Gather once over the world — style-agnostic; the candidate carries the raw below/above evidence.
        var candidates = MonumentSuggester.Gather(SignBelow(), Whole.Expand(2));
        await Assert.That(candidates.Any(c => (c.X, c.Y, c.Z) == (5, 8, 5) && c.Source == "sign" && c.PedestalId == 7)).IsTrue();

        // The SAME candidates score to the monument for the matching style…
        var hit = MonumentSuggester.Score(candidates, Whole, new MonumentStyle(PedestalKind.Bedrock, LabelKind.SignBelow)).Single();
        await Assert.That((hit.X, hit.Y, hit.Z)).IsEqualTo((5, 8, 5));
        await Assert.That(hit.Color).IsEqualTo("green");

        // …and to nothing for a mismatched pedestal — all from the stored candidates, no world re-read.
        await Assert.That(MonumentSuggester.Score(candidates, Whole, new MonumentStyle(PedestalKind.StainedGlass, LabelKind.SignBelow))).IsEmpty();
    }

    [Test]
    public async Task Box_excludes_signs_outside_it()
    {
        var box = new BlockBox(0, 0, 0, 3, 15, 15);   // the sign and its predicted cell (x=5) are outside MaxX=3
        var none = MonumentSuggester.Suggest(SignBelow(), box, new MonumentStyle(PedestalKind.Bedrock, LabelKind.SignBelow));
        await Assert.That(none).IsEmpty();
    }

    // ---- geometry: name-only armour stand marks the monument below it (dragons_hearth style) ----

    [Test]
    public async Task NameOnly_ArmorStand_marks_the_monument_below_it()
    {
        // bedrock at (5,7,5); monument air at (5,8,5); the stand floats above at feet y=11
        var world = Read([(5, 7, 5, 7, 0)],
            stands: [new ReadStand(5, 11.0, 5, HeadWool: null, CustomName: "§9§lBlue Wool Here!")]);

        var found = MonumentSuggester.Suggest(world, Whole,
            new MonumentStyle(PedestalKind.Bedrock, LabelKind.ArmorStand)).Single();
        await Assert.That((found.X, found.Y, found.Z)).IsEqualTo((5, 8, 5));
        await Assert.That(found.Source).IsEqualTo("armorstand");
        await Assert.That(found.Color).IsEqualTo("blue");
    }

    // ---- geometry: item frame holding wool marks the monument pocket (a_new_day style) ----

    [Test]
    public async Task WoolItemFrame_marks_the_capped_pocket_and_skips_a_floating_decorative_frame()
    {
        var world = Read(
        [
            (5, 7, 5, 139, 0),     // cobble pedestal; monument air at (5,8,5)
            (5, 9, 5, 44, 0),      // slab cap above it
            (10, 7, 10, 44, 0),    // a lone FLOATING slab — a decorative wool-frame support, no pocket
        ],
        frames:
        [
            new ReadFrame(5, 7, 5, "lime"),      // mounted on the pedestal; the monument is ABOVE it
            new ReadFrame(10, 7, 10, "lime"),    // mounted on the floating slab — decorative, no pocket
        ]);

        // Gather emits exactly one item-frame candidate — the real pocket — and rejects the floating one.
        var frameCands = MonumentSuggester.Gather(world, Whole.Expand(2)).Where(c => c.Source == "itemframe").ToList();
        await Assert.That(frameCands.Count).IsEqualTo(1);
        await Assert.That((frameCands[0].X, frameCands[0].Y, frameCands[0].Z)).IsEqualTo((5, 8, 5));

        var found = MonumentSuggester.Suggest(world, Whole, new MonumentStyle(PedestalKind.Any, LabelKind.ItemFrame)).Single();
        await Assert.That((found.X, found.Y, found.Z)).IsEqualTo((5, 8, 5));
        await Assert.That(found.Source).IsEqualTo("itemframe");
        await Assert.That(found.Color).IsEqualTo("lime");
    }

    // ---- geometry: high-confidence label-free monument (lupain style: distinctive pedestal AND cap) ----

    [Test]
    public async Task HighConf_geometry_detects_a_capped_label_free_monument_but_drops_the_single_signal_one()
    {
        var world = Read(
        [
            (5, 7, 5, 7, 0),       // bedrock pedestal; monument air at (5,8,5)
            (5, 9, 5, 95, 5),      // stained-glass cap, data 5 = lime
            (10, 7, 10, 7, 0),     // bedrock with OPEN air above → single-signal, must be dropped
        ]);

        // Only the capped (bedrock + glass) cell is gathered; the open-top bedrock is dropped as single-signal spray.
        var geom = MonumentSuggester.Gather(world, Whole.Expand(2)).Where(c => c.Source == "geometry").ToList();
        await Assert.That(geom.Count).IsEqualTo(1);
        await Assert.That((geom[0].X, geom[0].Y, geom[0].Z)).IsEqualTo((5, 8, 5));

        // The author declares bedrock + glass + no-label and gets the monument, at 0.60, coloured by the glass cap.
        var found = MonumentSuggester.Suggest(world, Whole,
            new MonumentStyle(PedestalKind.Bedrock, LabelKind.None, CapKind.StainedGlass)).Single();
        await Assert.That((found.X, found.Y, found.Z)).IsEqualTo((5, 8, 5));
        await Assert.That(found.Source).IsEqualTo("geometry");
        await Assert.That(found.Confidence).IsEqualTo(0.60);
        await Assert.That(found.Color).IsEqualTo("lime");
    }

    [Test]
    public async Task Geometry_requires_a_curated_cap_and_an_open_side()
    {
        var world = Read(
        [
            (3, 7, 3, 7, 0), (3, 9, 3, 95, 0),    // bedrock + glass, open sides → the only valid one
            (3, 7, 8, 7, 0), (3, 9, 8, 44, 0),    // bedrock + SLAB cap → cap not in the allowlist, dropped
            (8, 7, 3, 7, 0), (8, 9, 3, 95, 0),    // bedrock + glass but SEALED on all four sides → dropped
            (7, 8, 3, 1, 0), (9, 8, 3, 1, 0), (8, 8, 2, 1, 0), (8, 8, 4, 1, 0),
        ]);

        var geom = MonumentSuggester.Gather(world, Whole.Expand(2)).Where(c => c.Source == "geometry").ToList();
        await Assert.That(geom.Count).IsEqualTo(1);
        await Assert.That((geom[0].X, geom[0].Y, geom[0].Z)).IsEqualTo((3, 8, 3));
    }

    // ---- A6: only a monument-marker stand anchors the map (a rules/info stand must not suppress geometry) ----

    [Test]
    public async Task A_rules_stand_does_not_suppress_geometry_but_a_monument_label_stand_does()
    {
        // bedrock + glass label-free monument at (5,8,5), and one stand standing well away from it
        WorldReading With(string name) =>
            Read([(5, 7, 5, 7, 0), (5, 9, 5, 95, 0)],
                 stands: [new ReadStand(12, 5.0, 12, HeadWool: null, CustomName: name)]);

        // A rules/info stand (name is NOT a monument label) must NOT anchor the map → geometry still runs.
        var withRules = MonumentSuggester.Gather(With("Enemy Rushers may enter the middle room"), Whole.Expand(2));
        await Assert.That(withRules.Any(c => c.Source == "geometry" && (c.X, c.Y, c.Z) == (5, 8, 5))).IsTrue();

        // A monument-label-named stand DOES anchor the map → geometry suppressed.
        var withLabel = MonumentSuggester.Gather(With("Place Blue Wool here"), Whole.Expand(2));
        await Assert.That(withLabel.Any(c => c.Source == "geometry")).IsFalse();
    }
}
