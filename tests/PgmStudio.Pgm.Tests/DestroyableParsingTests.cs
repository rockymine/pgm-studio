using PgmStudio.Pgm;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// The DTM XML surface: <c>&lt;destroyables&gt;</c> leaves and the <c>&lt;modes&gt;</c> they reference.
/// A destroyable is <c>owner + region + materials</c> — no wiring, no spawner, no per-capturing-team
/// fan-out. Attributes cascade from the enclosing groups, which real maps depend on heavily.
/// </summary>
public sealed class DestroyableParsingTests
{
    private static string Map(string body) =>
        $"""<?xml version="1.0"?><map proto="1.5.0"><name>m</name><version>1</version><objective>o</objective>{body}</map>""";

    private static PgmStudio.Domain.MapXml Parse(string body) => MapParser.ParseXmlString(Map(body));

    // ── attribute inheritance ───────────────────────────────────────────────────────
    [Test]
    public async Task Group_attributes_cascade_to_every_leaf()
    {
        var m = Parse("""
            <destroyables materials="obsidian" mode-changes="true" completion="90%">
                <destroyables name="Hill Monument">
                    <destroyable owner="green"><region><cuboid min="20,43,146" max="23,46,149"/></region></destroyable>
                    <destroyable owner="orange"><region><cuboid min="-19,43,-183" max="-22,46,-186"/></region></destroyable>
                </destroyables>
            </destroyables>
            """);

        await Assert.That(m.Destroyables.Count).IsEqualTo(2);
        foreach (var d in m.Destroyables)
        {
            await Assert.That(d.Name).IsEqualTo("Hill Monument");
            await Assert.That(d.Materials).IsEqualTo("obsidian");
            await Assert.That(d.ModeChanges).IsTrue();
            await Assert.That(d.Completion).IsEqualTo(0.9);
        }
        await Assert.That(m.Destroyables.Select(d => d.Owner)).IsEquivalentTo(new[] { "green", "orange" });
    }

    [Test]
    public async Task A_leaf_attribute_beats_the_inherited_one()
    {
        var m = Parse("""
            <destroyables materials="obsidian" owner="green">
                <destroyable name="a"/>
                <destroyable name="b" materials="emerald block" owner="orange"/>
            </destroyables>
            """);

        await Assert.That(m.Destroyables[0].Materials).IsEqualTo("obsidian");
        await Assert.That(m.Destroyables[0].Owner).IsEqualTo("green");
        await Assert.That(m.Destroyables[1].Materials).IsEqualTo("emerald block");
        await Assert.That(m.Destroyables[1].Owner).IsEqualTo("orange");
    }

    // Wools flatten through the same helper, and real maps put a wool's colour and location on the group.
    [Test]
    public async Task A_wool_inherits_its_colour_and_location_from_its_group()
    {
        var m = Parse("""
            <wools craftable="false">
                <wools color="cyan" location="61,62,-113">
                    <wool team="lime" monument="lime-cyan"/>
                    <wool team="orange" monument="orange-cyan"/>
                </wools>
            </wools>
            """);

        await Assert.That(m.Wools.Count).IsEqualTo(2);
        foreach (var w in m.Wools)
        {
            await Assert.That(w.Color).IsEqualTo("cyan");
            await Assert.That(w.Location.X).IsEqualTo(61);
            await Assert.That(w.Location.Z).IsEqualTo(-113);
        }
    }

    // ── the materials attribute ─────────────────────────────────────────────────────
    [Test]
    public async Task Materials_accepts_both_spellings_preferring_the_plural()
    {
        await Assert.That(Parse("""<destroyables><destroyable owner="red" name="a" material="obsidian"/></destroyables>""")
            .Destroyables[0].Materials).IsEqualTo("obsidian");
        await Assert.That(Parse("""<destroyables><destroyable owner="red" name="a" materials="gold block" material="obsidian"/></destroyables>""")
            .Destroyables[0].Materials).IsEqualTo("gold block");
    }

    // ── completion ──────────────────────────────────────────────────────────────────
    // PGM's parsePercent strips any '%' and divides by 100, so the value is a percentage either way.
    [Test]
    [Arguments("90%", 0.9)]
    [Arguments("90", 0.9)]
    [Arguments("100%", 1.0)]
    [Arguments("0%", 0.0)]
    [Arguments("0.8", 0.008)]
    public async Task Completion_is_always_a_percentage_sign_or_not(string raw, double expected)
    {
        var m = Parse($"""<destroyables><destroyable owner="red" name="a" materials="obsidian" completion="{raw}"/></destroyables>""");
        await Assert.That(m.Destroyables[0].Completion).IsEqualTo(expected);
    }

    [Test]
    public async Task An_unauthored_completion_stays_null_meaning_the_whole_structure()
    {
        var m = Parse("""<destroyables><destroyable owner="red" name="a" materials="obsidian"/></destroyables>""");
        await Assert.That(m.Destroyables[0].Completion).IsNull();
    }

    // ── mode membership: a tri-state, not a list ────────────────────────────────────
    [Test]
    public async Task Mode_changes_means_every_mode_and_lists_none()
    {
        var m = Parse("""<destroyables><destroyable owner="red" name="a" materials="obsidian" mode-changes="true"/></destroyables>""");
        await Assert.That(m.Destroyables[0].ModeChanges).IsTrue();
        await Assert.That(m.Destroyables[0].Modes).IsNull();
    }

    [Test]
    public async Task An_explicit_mode_set_is_kept_in_order()
    {
        var m = Parse("""
            <modes><mode id="a" after="10m" material="beacon"/><mode id="b" after="20m" material="coal block"/></modes>
            <destroyables><destroyable owner="red" name="a" materials="obsidian" modes="a b"/></destroyables>
            """);
        await Assert.That(m.Destroyables[0].ModeChanges).IsFalse();
        await Assert.That(m.Destroyables[0].Modes).IsEquivalentTo(new[] { "a", "b" });
    }

    [Test]
    public async Task Declaring_neither_means_no_modes()
    {
        var m = Parse("""<destroyables><destroyable owner="red" name="a" materials="obsidian"/></destroyables>""");
        await Assert.That(m.Destroyables[0].ModeChanges).IsFalse();
        await Assert.That(m.Destroyables[0].Modes).IsNull();
    }

    [Test]
    public async Task Combining_modes_with_mode_changes_is_rejected()
    {
        await Assert.That(() => Parse("""
            <modes><mode id="a" after="10m" material="beacon"/></modes>
            <destroyables><destroyable owner="red" name="a" materials="obsidian" modes="a" mode-changes="true"/></destroyables>
            """)).Throws<UnsupportedMapException>();
    }

    // ── the region property ─────────────────────────────────────────────────────────
    [Test]
    public async Task The_region_resolves_from_an_attribute_or_a_child()
    {
        var byAttr = Parse("""
            <regions><cuboid id="mon" min="20,43,146" max="23,46,149"/></regions>
            <destroyables><destroyable owner="red" name="a" materials="obsidian" region="mon"/></destroyables>
            """);
        await Assert.That(byAttr.Destroyables[0].RegionId).IsEqualTo("mon");

        var byChild = Parse("""<destroyables><destroyable owner="red" name="a" materials="obsidian"><region><block>-4,9,30</block></region></destroyable></destroyables>""");
        var region = byChild.Regions[byChild.Destroyables[0].RegionId];
        await Assert.That(region.Type).IsEqualTo("block");
        await Assert.That(region.PosY).IsEqualTo(9);
    }

    // A <region> wrapper is the union of everything inside it, so a multi-shape wrapper keeps every shape.
    [Test]
    public async Task A_multi_shape_region_wrapper_becomes_a_union()
    {
        var m = Parse("""
            <destroyables><destroyable owner="red" name="a" materials="obsidian"><region>
                <cuboid min="0,0,0" max="2,2,2"/>
                <cuboid min="8,0,0" max="10,2,2"/>
            </region></destroyable></destroyables>
            """);
        var region = m.Regions[m.Destroyables[0].RegionId];
        await Assert.That(region.Type).IsEqualTo("union");
        await Assert.That(region.Children!.Count).IsEqualTo(2);
    }

    // ── ids ─────────────────────────────────────────────────────────────────────────
    [Test]
    public async Task An_unauthored_id_is_generated_from_the_owner_and_name_and_stays_unique()
    {
        var m = Parse("""
            <destroyables materials="obsidian">
                <destroyable owner="green" name="Hill Monument"/>
                <destroyable owner="green" name="Hill Monument"/>
                <destroyable owner="orange" name="River Monument" id="explicit"/>
            </destroyables>
            """);
        await Assert.That(m.Destroyables.Select(d => d.Id))
            .IsEquivalentTo(new[] { "green-hill-monument", "green-hill-monument-2", "explicit" });
    }

    // ── modes ───────────────────────────────────────────────────────────────────────
    [Test]
    public async Task Modes_parse_their_schedule_and_material()
    {
        var m = Parse("""
            <modes>
                <mode after="25m" material="beacon" name="`bBEACON MONUMENT MODE"/>
                <mode after="45m" material="coal block" name="`8COAL MONUMENT MODE" show-before="30s"/>
            </modes>
            """);
        await Assert.That(m.Modes.Count).IsEqualTo(2);
        await Assert.That(m.Modes[0].After).IsEqualTo("25m");
        await Assert.That(m.Modes[0].Material).IsEqualTo("beacon");
        await Assert.That(m.Modes[1].ShowBefore).IsEqualTo("30s");
    }

    // An unauthored id comes from the name with its colour codes stripped, so `modes="…" always resolves.
    [Test]
    public async Task An_unauthored_mode_id_is_generated_from_its_name()
    {
        var m = Parse("""<modes><mode after="25m" material="beacon" name="`bBEACON MONUMENT MODE"/></modes>""");
        await Assert.That(m.Modes[0].Id).IsEqualTo("mode-beacon-monument-mode");
    }

    [Test]
    public async Task An_unnamed_mode_takes_its_id_from_its_material()
    {
        var m = Parse("""<modes><mode after="0s" material="air"/></modes>""");
        await Assert.That(m.Modes[0].Id).IsEqualTo("mode-air");
    }

    // boss-bar="false" is PGM's spelling of "no countdown".
    [Test]
    public async Task Boss_bar_false_means_no_countdown()
    {
        var m = Parse("""<modes><mode id="m" after="10m" material="air" boss-bar="false"/></modes>""");
        await Assert.That(m.Modes[0].ShowBefore).IsEqualTo("0s");
    }

    // ── show ────────────────────────────────────────────────────────────────────────
    [Test]
    public async Task Show_defaults_to_true_and_round_trips_false()
    {
        await Assert.That(Parse("""<destroyables><destroyable owner="red" name="a" materials="obsidian"/></destroyables>""")
            .Destroyables[0].Show).IsTrue();
        await Assert.That(Parse("""<destroyables><destroyable owner="red" name="a" materials="obsidian" show="false"/></destroyables>""")
            .Destroyables[0].Show).IsFalse();
    }

    // ── phantoms: a destroyable is not always an objective ──────────────────────────
    [Test]
    public async Task A_visible_destroyable_is_an_objective()
    {
        var d = Parse("""<destroyables><destroyable owner="red" name="Hill" materials="obsidian"/></destroyables>""").Destroyables[0];
        await Assert.That(d.IsObjective).IsTrue();
        await Assert.That(d.Phantom).IsEqualTo(PgmStudio.Domain.PhantomKind.None);
    }

    // The pre-game build floor: stained glass at the world floor, erased at match start by a 0s → air mode
    // while a void filter defines the real build region. abstract gives both "owners" the identical region,
    // which is what vestigial ownership looks like.
    [Test]
    public async Task A_hidden_destroyable_carrying_a_mode_is_a_block_swap()
    {
        var m = Parse("""
            <modes><mode id="mode-air" after="0s" material="air"/></modes>
            <destroyables materials="stained glass" completion="0%" show="false" mode-changes="true">
                <destroyable owner="red" name="monu"/>
                <destroyable owner="blue" name="monu"/>
            </destroyables>
            """);
        foreach (var d in m.Destroyables)
        {
            await Assert.That(d.IsObjective).IsFalse();
            await Assert.That(d.Phantom).IsEqualTo(PgmStudio.Domain.PhantomKind.BlockSwap);
        }
    }

    // deathrun_aperture's ten levers: hidden, no mode — broken to fire a filter.
    [Test]
    public async Task A_hidden_destroyable_with_no_mode_is_a_trigger()
    {
        var d = Parse("""<destroyables><destroyable owner="red" name="leverarrow1" materials="lever" show="false"/></destroyables>""").Destroyables[0];
        await Assert.That(d.IsObjective).IsFalse();
        await Assert.That(d.Phantom).IsEqualTo(PgmStudio.Domain.PhantomKind.Trigger);
    }

    // Neither of these identifies a phantom on its own: most non-required destroyables are genuine, and
    // gold_in_them_thar_kills is a real objective that completes at 50% while crumbling to air.
    [Test]
    public async Task Completion_and_required_do_not_make_a_destroyable_a_phantom()
    {
        var d = Parse("""<destroyables><destroyable owner="red" name="a" materials="gold block" completion="50%" required="false" show="true"/></destroyables>""").Destroyables[0];
        await Assert.That(d.IsObjective).IsTrue();
        await Assert.That(d.Completion).IsEqualTo(0.5);
    }
}
