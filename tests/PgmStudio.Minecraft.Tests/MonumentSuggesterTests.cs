using fNbt;
using PgmStudio.Domain;
using PgmStudio.Minecraft;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// The authoring-flow monument suggester. Pure-function text tests plus synthetic-chunk geometry tests
/// for the dominant styles. Corpus precision/recall (thunder/pigland/dragons_hearth → 100% with the
/// style declared) is covered by the RoundTrip <c>--suggest-monuments</c> harness.
/// </summary>
public class MonumentSuggesterTests
{
    private static int Idx(int x, int y, int z) => (y << 8) | (z << 4) | x;
    private static void SetNibble(byte[] p, int i, int v)
    {
        var b = i >> 1;
        p[b] = (i & 1) == 0 ? (byte)((p[b] & 0xF0) | (v & 0x0F)) : (byte)((p[b] & 0x0F) | ((v & 0x0F) << 4));
    }
    private static readonly BlockBox Whole = new(0, 0, 0, 15, 15, 15);

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

    private static AnvilRegion.Chunk SignBelowChunk()
    {
        var blocks = new byte[4096];
        var data = new byte[2048];
        blocks[Idx(5, 7, 5)] = 7;                                   // bedrock pedestal, directly below monument
        blocks[Idx(5, 7, 6)] = 68; SetNibble(data, Idx(5, 7, 6), 3); // wall sign at dz+1, data 3 = faces -z (toward monument)
        // (5,8,5) left air = the monument placement cell
        var section = new NbtCompound { new NbtByte("Y", 0), new NbtByteArray("Blocks", blocks), new NbtByteArray("Data", data) };
        var sign = new NbtCompound
        {
            new NbtString("id", "Sign"), new NbtInt("x", 5), new NbtInt("y", 7), new NbtInt("z", 6),
            new NbtString("Text1", "{\"text\":\"\"}"),
            new NbtString("Text2", "{\"extra\":[{\"color\":\"dark_green\",\"text\":\"Green Wool\"}],\"text\":\"\"}"),
            new NbtString("Text3", "{\"text\":\"\"}"), new NbtString("Text4", "{\"text\":\"\"}"),
        };
        return new AnvilRegion.Chunk(0, 0, new NbtCompound("Level")
        {
            new NbtList("Sections", new[] { section }), new NbtList("TileEntities", new[] { sign }),
        });
    }

    [Test]
    public async Task SignBelow_predicts_the_air_cell_above_the_pedestal()
    {
        var s = MonumentSuggester.Suggest([SignBelowChunk()], Whole,
            new MonumentStyle(PedestalKind.Bedrock, LabelKind.SignBelow)).Single();
        await Assert.That((s.X, s.Y, s.Z)).IsEqualTo((5, 8, 5));
        await Assert.That(s.Color).IsEqualTo("green");
        await Assert.That(s.Source).IsEqualTo("sign");
        await Assert.That(s.PedestalId).IsEqualTo(7);
    }

    [Test]
    public async Task Declared_pedestal_filters_out_the_wrong_style()
    {
        var none = MonumentSuggester.Suggest([SignBelowChunk()], Whole,
            new MonumentStyle(PedestalKind.StainedGlass, LabelKind.SignBelow));
        await Assert.That(none).IsEmpty();   // pedestal is bedrock, not glass
    }

    [Test]
    public async Task Gather_is_style_agnostic_and_Score_applies_the_style(/* F9 */)
    {
        // Gather once over the world — style-agnostic; the candidate carries the raw below/above evidence.
        var candidates = MonumentSuggester.Gather([SignBelowChunk()], Whole.Expand(2));
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
        var none = MonumentSuggester.Suggest([SignBelowChunk()], box, new MonumentStyle(PedestalKind.Bedrock, LabelKind.SignBelow));
        await Assert.That(none).IsEmpty();
    }

    // ---- geometry: name-only armour stand marks the monument below it (dragons_hearth style) ----

    [Test]
    public async Task NameOnly_ArmorStand_marks_the_monument_below_it()
    {
        var blocks = new byte[4096];
        blocks[Idx(5, 7, 5)] = 7;   // bedrock; monument air at (5,8,5); stand floats above at feet y=11
        var section = new NbtCompound { new NbtByte("Y", 0), new NbtByteArray("Blocks", blocks), new NbtByteArray("Data", new byte[2048]) };
        var stand = new NbtCompound
        {
            new NbtString("id", "ArmorStand"),
            new NbtList("Pos", new[] { new NbtDouble(5.5), new NbtDouble(11.0), new NbtDouble(5.5) }),
            new NbtString("CustomName", "§9§lBlue Wool Here!"),
        };
        var chunk = new AnvilRegion.Chunk(0, 0, new NbtCompound("Level")
        {
            new NbtList("Sections", new[] { section }), new NbtList("Entities", new[] { stand }),
        });
        var s = MonumentSuggester.Suggest([chunk], Whole, new MonumentStyle(PedestalKind.Bedrock, LabelKind.ArmorStand)).Single();
        await Assert.That((s.X, s.Y, s.Z)).IsEqualTo((5, 8, 5));
        await Assert.That(s.Source).IsEqualTo("armorstand");
        await Assert.That(s.Color).IsEqualTo("blue");
    }

    // ---- geometry: item frame holding wool marks the monument pocket (a_new_day style) ----

    private static NbtCompound WoolFrame(int tx, int ty, int tz, int facing, int damage) => new()
    {
        new NbtString("id", "ItemFrame"),
        new NbtList("Pos", new[] { new NbtDouble(tx + 0.5), new NbtDouble(ty + 0.5), new NbtDouble(tz + 0.5) }),
        new NbtInt("TileX", tx), new NbtInt("TileY", ty), new NbtInt("TileZ", tz), new NbtByte("Facing", (byte)facing),
        new NbtCompound("Item") { new NbtString("id", "minecraft:wool"), new NbtShort("Damage", (short)damage), new NbtByte("Count", 1) },
    };

    [Test]
    public async Task WoolItemFrame_marks_the_capped_pocket_and_skips_a_floating_decorative_frame()
    {
        var blocks = new byte[4096];
        blocks[Idx(5, 7, 5)] = 139;   // cobble pedestal; monument air at (5,8,5); slab cap above
        blocks[Idx(5, 9, 5)] = 44;
        blocks[Idx(10, 7, 10)] = 44;  // a lone FLOATING slab — a decorative wool-frame support, no pocket
        var section = new NbtCompound { new NbtByte("Y", 0), new NbtByteArray("Blocks", blocks), new NbtByteArray("Data", new byte[2048]) };
        var chunk = new AnvilRegion.Chunk(0, 0, new NbtCompound("Level")
        {
            new NbtList("Sections", new[] { section }),
            new NbtList("Entities", new[]
            {
                WoolFrame(6, 7, 5, 3, 5),       // mounted on the pedestal (support = Tile+(-1,0) = (5,7,5)); monument ABOVE; lime
                WoolFrame(11, 7, 10, 3, 5),     // mounted on the floating slab (10,7,10) — decorative, no monument pocket
            }),
        });

        // Gather emits exactly one item-frame candidate — the real pocket — and rejects the floating one.
        var frameCands = MonumentSuggester.Gather([chunk], Whole.Expand(2)).Where(c => c.Source == "itemframe").ToList();
        await Assert.That(frameCands.Count).IsEqualTo(1);
        await Assert.That((frameCands[0].X, frameCands[0].Y, frameCands[0].Z)).IsEqualTo((5, 8, 5));

        var s = MonumentSuggester.Suggest([chunk], Whole, new MonumentStyle(PedestalKind.Any, LabelKind.ItemFrame)).Single();
        await Assert.That((s.X, s.Y, s.Z)).IsEqualTo((5, 8, 5));
        await Assert.That(s.Source).IsEqualTo("itemframe");
        await Assert.That(s.Color).IsEqualTo("lime");
    }

    // ---- geometry: high-confidence label-free monument (lupain style: distinctive pedestal AND cap) ----

    [Test]
    public async Task HighConf_geometry_detects_a_capped_label_free_monument_but_drops_the_single_signal_one()
    {
        var blocks = new byte[4096];
        var data = new byte[2048];
        blocks[Idx(5, 7, 5)] = 7;                                    // bedrock pedestal; monument air at (5,8,5)
        blocks[Idx(5, 9, 5)] = 95; SetNibble(data, Idx(5, 9, 5), 5); // stained-glass cap, data 5 = lime
        blocks[Idx(10, 7, 10)] = 7;                                  // bedrock with OPEN air above -> single-signal, must be dropped
        var section = new NbtCompound { new NbtByte("Y", 0), new NbtByteArray("Blocks", blocks), new NbtByteArray("Data", data) };
        var chunk = new AnvilRegion.Chunk(0, 0, new NbtCompound("Level") { new NbtList("Sections", new[] { section }) });

        // Only the capped (bedrock + glass) cell is gathered; the open-top bedrock is dropped as single-signal spray.
        var geom = MonumentSuggester.Gather([chunk], Whole.Expand(2)).Where(c => c.Source == "geometry").ToList();
        await Assert.That(geom.Count).IsEqualTo(1);
        await Assert.That((geom[0].X, geom[0].Y, geom[0].Z)).IsEqualTo((5, 8, 5));

        // The author declares bedrock + glass + no-label and gets the monument, at 0.60, coloured by the glass cap.
        var s = MonumentSuggester.Suggest([chunk], Whole,
            new MonumentStyle(PedestalKind.Bedrock, LabelKind.None, CapKind.StainedGlass)).Single();
        await Assert.That((s.X, s.Y, s.Z)).IsEqualTo((5, 8, 5));
        await Assert.That(s.Source).IsEqualTo("geometry");
        await Assert.That(s.Confidence).IsEqualTo(0.60);
        await Assert.That(s.Color).IsEqualTo("lime");
    }

    [Test]
    public async Task Geometry_requires_a_curated_cap_and_an_open_side()
    {
        var blocks = new byte[4096];
        blocks[Idx(3, 7, 3)] = 7; blocks[Idx(3, 9, 3)] = 95;   // bedrock + glass, open sides -> the only valid one
        blocks[Idx(3, 7, 8)] = 7; blocks[Idx(3, 9, 8)] = 44;   // bedrock + SLAB cap -> cap not in the allowlist, dropped
        blocks[Idx(8, 7, 3)] = 7; blocks[Idx(8, 9, 3)] = 95;   // bedrock + glass but SEALED on all 4 sides -> dropped
        blocks[Idx(7, 8, 3)] = 1; blocks[Idx(9, 8, 3)] = 1; blocks[Idx(8, 8, 2)] = 1; blocks[Idx(8, 8, 4)] = 1;
        var section = new NbtCompound { new NbtByte("Y", 0), new NbtByteArray("Blocks", blocks), new NbtByteArray("Data", new byte[2048]) };
        var chunk = new AnvilRegion.Chunk(0, 0, new NbtCompound("Level") { new NbtList("Sections", new[] { section }) });

        var geom = MonumentSuggester.Gather([chunk], Whole.Expand(2)).Where(c => c.Source == "geometry").ToList();
        await Assert.That(geom.Count).IsEqualTo(1);
        await Assert.That((geom[0].X, geom[0].Y, geom[0].Z)).IsEqualTo((3, 8, 3));
    }

    // ---- A6: only a monument-marker stand anchors the map (a rules/info stand must not suppress geometry) ----

    [Test]
    public async Task A_rules_stand_does_not_suppress_geometry_but_a_monument_label_stand_does()
    {
        var blocks = new byte[4096];
        blocks[Idx(5, 7, 5)] = 7; blocks[Idx(5, 9, 5)] = 95;   // bedrock + glass label-free monument at (5,8,5)

        NbtCompound Stand(string name) => new()
        {
            new NbtString("id", "ArmorStand"),
            new NbtList("Pos", new[] { new NbtDouble(12.5), new NbtDouble(5.0), new NbtDouble(12.5) }),
            new NbtString("CustomName", name),
        };
        // Fresh Sections compound per chunk — an fNbt tag can't belong to two parents.
        AnvilRegion.Chunk Chunk(NbtCompound stand) => new(0, 0, new NbtCompound("Level")
        {
            new NbtList("Sections", new[] { new NbtCompound { new NbtByte("Y", 0), new NbtByteArray("Blocks", blocks), new NbtByteArray("Data", new byte[2048]) } }),
            new NbtList("Entities", new[] { stand }),
        });

        // A rules/info stand (name is NOT a monument label) must NOT anchor the map → geometry still runs.
        var withRules = MonumentSuggester.Gather([Chunk(Stand("Enemy Rushers may enter the middle room"))], Whole.Expand(2));
        await Assert.That(withRules.Any(c => c.Source == "geometry" && (c.X, c.Y, c.Z) == (5, 8, 5))).IsTrue();

        // A monument-label-named stand DOES anchor the map → geometry suppressed.
        var withLabel = MonumentSuggester.Gather([Chunk(Stand("Place Blue Wool here"))], Whole.Expand(2));
        await Assert.That(withLabel.Any(c => c.Source == "geometry")).IsFalse();
    }
}
