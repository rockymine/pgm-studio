using PgmStudio.Geom.Algorithms;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Minecraft.Houses;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// The dressing document's serialization (B130): a parse failure anywhere in the document has to surface,
/// naming the prop and the field, rather than being read as though the map carried no props at all. Every
/// "throws" assertion below is the invariant the old code broke — before the fix, each of these inputs
/// silently produced <see cref="DressingDoc.Empty"/> (or, for a bare prop, <c>null</c>) and a 200 export.
/// </summary>
public sealed class DressingJsonTests
{
    // ── the shapes that still read as "nothing placed" ────────────────────────────────────────────────
    [Test]
    public async Task An_empty_document_reads_as_no_props_rather_than_a_fault()
    {
        // A map that never opened the phase carries no `props` key at all — that is not malformed, it is
        // the phase's own empty state, and it must not be confused with a document that tried to say
        // something and failed.
        await Assert.That(DressingJson.Deserialize("{}").Props).IsEmpty();
    }

    [Test]
    public async Task A_document_of_every_kind_round_trips()
    {
        var json = DressingJson.Serialize(new DressingDoc
        {
            Props =
            [
                new StrokeProp { Id = "p", Seed = 1, Points = [[0, 0], [10, 10]], Pave = new SolidMaterial(Blocks.Gravel) },
                new WaterProp { Id = "w", Seed = 2, Points = [[0, 0], [10, 10]] },
                new TreeProp { Id = "t", Seed = 3, X = 1, Z = 1 },
                new BoulderProp { Id = "b", Seed = 4, X = 2, Z = 2 },
                new FloraProp { Id = "f", Seed = 5, Points = [[0, 0], [10, 0], [10, 10]] },
                new HouseProp { Id = "h", Seed = 6, Wings = [new AuthoredWing([[0, 0], [5, 5]])] },
            ],
        });

        var back = DressingJson.Deserialize(json);
        await Assert.That(DressingJson.Serialize(back)).IsEqualTo(json);
    }

    // ── the first-key constraint (B130) ────────────────────────────────────────────────────────────────
    [Test]
    public async Task A_copied_tree_round_trips_with_its_body_and_names_its_key_by_its_block_count()
    {
        var doc = new DressingDoc
        {
            Props = [new TreeProp { Id = "t", Seed = 3, X = 1, Z = 1, StyleKey = "cut",
                                    Style = new TreeStyle { Form = TreeForm.Copied, Body = [[0, 0, 0, 17, 12], [0, 1, 0, 18, 4]] } }],
            Styles = { ["cut"] = new TreeStyle { Form = TreeForm.Copied, Body = [[0, 0, 0, 17, 12], [0, 1, 0, 18, 4]] } },
        };
        var json = DressingJson.Serialize(doc);
        await Assert.That(json).Contains("\"form\":\"copied\"");
        await Assert.That(json).Contains("\"body\":[[0,0,0,17,12],[0,1,0,18,4]]");
        var back = DressingJson.Deserialize(json);
        var tree = (TreeProp)back.Props[0];
        await Assert.That(tree.Style.Form).IsEqualTo(TreeForm.Copied);
        await Assert.That(tree.Style.BodyHeight).IsEqualTo(2);
        await Assert.That(tree.Style.BodyCells.Count()).IsEqualTo(2);

        // Stated inline rather than under a key — the fields spread on the placement, the way a document
        // written before recipes had names states them — the recipe is named by what it is and how much of
        // it there is.
        var inline = DressingJson.Deserialize("""
            {"props":[{"kind":"tree","id":"t","x":1,"z":1,"seed":3,
              "form":"copied","body":[[0,0,0,17,12],[0,1,0,18,4]]}]}
            """);
        await Assert.That(((TreeProp)inline.Props[0]).StyleKey).IsEqualTo("copied-2");
    }

    [Test]
    public async Task A_props_kind_reads_regardless_of_where_it_falls_in_the_object()
    {
        // `kind` last rather than first — a hand-edited or LLM-authored document has no reason to prefer one
        // key order over another, so the reader must not either.
        var doc = DressingJson.Deserialize(
            """{"props":[{"id":"p1","seed":1,"x":0,"z":0,"kind":"tree"}]}""");
        await Assert.That(doc.Props.Count).IsEqualTo(1);
        await Assert.That(doc.Props[0]).IsTypeOf<TreeProp>();
    }

    [Test]
    public async Task A_nested_materials_kind_also_reads_out_of_order()
    {
        var doc = DressingJson.Deserialize(
            """{"props":[{"kind":"stroke","id":"p1","seed":1,"points":[[0,0],[1,1]],"pave":{"id":13,"data":0,"kind":"solid"}}]}""");
        await Assert.That(((StrokeProp)doc.Props[0]).Pave).IsEqualTo((TerrainMaterial)new SolidMaterial(13));
    }

    // ── the house upgrade (G177) ───────────────────────────────────────────────────────────────────────
    [Test]
    public async Task A_stored_house_with_two_corners_reads_as_one_wing()
    {
        // A placed building was exactly two corners before G177; a document a map already carries — or one
        // written against the old shape by hand — still has to build the same house.
        var doc = DressingJson.Deserialize(
            """{"props":[{"kind":"house","id":"h1","seed":1,"points":[[0,0],[10,6]]}]}""");
        var house = (HouseProp)doc.Props[0];

        await Assert.That(house.Wings.Count).IsEqualTo(1);
        var plan = house.Plan();
        await Assert.That(plan).IsNotNull();
        await Assert.That((plan!.MinX, plan.MinZ, plan.Width, plan.Depth)).IsEqualTo((0, 0, 11, 7));
    }

    /// <summary>
    /// <b>A wing that was a bare corner pair still builds the same house.</b> Every wing in every stored
    /// document was two corners and nothing else, which meant "wear the building's own everything" — and that
    /// is exactly what an entry stating nothing means now, so the upgrade is the corner pair moved under a
    /// key rather than any change to what it builds.
    /// </summary>
    [Test]
    public async Task A_wing_that_states_nothing_reads_as_a_wing_that_states_nothing()
    {
        var doc = DressingJson.Deserialize(
            """{"props":[{"kind":"house","id":"h1","seed":1,"wings":[[[0,0],[10,6]],[[0,7],[5,13]]]}]}""");
        var house = (HouseProp)doc.Props[0];

        await Assert.That(house.Wings.Count).IsEqualTo(2);
        await Assert.That(house.Wings[0].Spec).IsEqualTo(default(WingSpec));
        await Assert.That(house.Check()).IsEmpty();

        var plan = house.Plan()!;
        await Assert.That(plan.Wings[1].Projects).IsFalse();
        await Assert.That(plan.Wings[1].Ridge).IsNull();
    }

    /// <summary>
    /// <b>What a wing states reaches the building it raises.</b> The six are the whole of what a wing can say
    /// about itself apart from where it stands, and until now none of them could be written down: a document
    /// held two corners, so every authored building marched and the second gable was unreachable.
    /// </summary>
    [Test]
    public async Task A_wing_states_its_own_storeys_roof_ridge_and_joint()
    {
        var doc = DressingJson.Deserialize(
            """
            {"props":[{"kind":"house","id":"h1","seed":1,"wings":[
              {"corners":[[0,6],[9,10]]},
              {"corners":[[0,0],[4,5]],"spec":{"storeysHigh":2,"form":"Hip","pitch":2,"roofSlab":44,
                                               "ridge":"AlongZ","projects":true}}
            ]}]}
            """);
        var wing = ((HouseProp)doc.Props[0]).Plan()!.Wings[1];

        await Assert.That((wing.StoreysHigh, wing.Pitch, wing.RoofSlab)).IsEqualTo((2, 2, (int?)44));
        await Assert.That((wing.Form, wing.Ridge, wing.Projects))
            .IsEqualTo(((RoofForm?)RoofForm.Hip, (RidgeAxis?)RidgeAxis.AlongZ, true));

        // And the round trip is the document: what is written is what reads back.
        var again = (HouseProp)DressingJson.Deserialize(DressingJson.Serialize(doc)).Props[0];
        await Assert.That(again.Wings[1].Spec).IsEqualTo(((HouseProp)doc.Props[0]).Wings[1].Spec);
    }

    /// <summary>
    /// <b>A style is snapshotted in two places, so it is read forward in both.</b> A house prop carries a
    /// whole style, and a style read straight off a stored document rather than through its own upgrade keeps
    /// whatever fields the record still happens to name and silently drops the rest — a building that quietly
    /// stops looking like itself, which is the one failure a snapshot's upgrade exists to prevent.
    /// </summary>
    [Test]
    public async Task A_house_props_style_is_read_forward_the_way_a_standalone_one_is()
    {
        var doc = DressingJson.Deserialize(
            """
            {"props":[{"kind":"house","id":"h1","seed":1,"wings":[[[0,0],[10,6]]],
              "style":{"overhang":2,"form":"hip","roof":{"kind":"solid","id":98,"data":0},
                       "sill":{"kind":"solid","id":4,"data":0}}}]}
            """);
        var style = ((HouseProp)doc.Props[0]).Style;

        await Assert.That(style.Roof.Overhang).IsEqualTo(2);
        await Assert.That(style.Roof.Form).IsEqualTo(RoofForm.Hip);
        await Assert.That(style.Roof.Body).IsEqualTo((TerrainMaterial)new SolidMaterial(98));
        await Assert.That(style.Foundation.Footing).IsEqualTo((TerrainMaterial?)new SolidMaterial(Blocks.Cobblestone));
    }

    // ── enum case (B130) ───────────────────────────────────────────────────────────────────────────────
    [Test]
    public async Task An_enum_value_reads_in_any_case_the_converter_is_given()
    {
        // The corpus this bug was found against writes `style`/`form` in the raw C# member spelling
        // (PascalCase) rather than the documented camelCase wire form. The converter tolerates either, so
        // this is not a fault the parser has to refuse — only the documentation had to settle on one case.
        var doc = DressingJson.Deserialize(
            """{"props":[{"kind":"water","id":"w1","seed":1,"points":[[0,0],[1,1]],"form":"Natural"}]}""");
        await Assert.That(((WaterProp)doc.Props[0]).Form).IsEqualTo(ChannelForm.Natural);
    }

    // ── what still has to refuse ───────────────────────────────────────────────────────────────────────
    [Test]
    public async Task An_unknown_kind_names_the_prop_and_lists_the_kinds_it_could_have_been()
    {
        var ex = Assert.Throws<DressingParseException>(() =>
            DressingJson.Deserialize("""{"props":[{"kind":"tren","id":"d7","seed":1,"x":0,"z":0}]}"""));

        await Assert.That(ex.Subject).Contains("d7");
        await Assert.That(ex.Field).IsEqualTo("kind");
        await Assert.That(ex.Message).Contains("tren");
        await Assert.That(ex.Message).Contains("stroke");   // one of the six known prop kinds
    }

    [Test]
    public async Task A_missing_kind_is_named_as_missing_rather_than_as_a_generic_parse_failure()
    {
        var ex = Assert.Throws<DressingParseException>(() =>
            DressingJson.Deserialize("""{"props":[{"id":"d8","seed":1,"x":0,"z":0}]}"""));

        await Assert.That(ex.Field).IsEqualTo("kind");
        await Assert.That(ex.Message).Contains("d8");
    }

    [Test]
    public async Task A_malformed_field_names_the_prop_and_the_field_rather_than_the_raw_codec_text()
    {
        var ex = Assert.Throws<DressingParseException>(() =>
            DressingJson.Deserialize(
                """{"props":[{"kind":"stroke","id":"d9","seed":1,"points":[[0,0],[1,1]],"radius":"wide"}]}"""));

        await Assert.That(ex.Subject).Contains("d9");
        await Assert.That(ex.Field).IsEqualTo("radius");
        await Assert.That(ex.Message).DoesNotContain("System.Text.Json");   // no raw codec noise
    }

    [Test]
    public async Task One_bad_prop_costs_the_whole_document_rather_than_being_skipped_silently()
    {
        // The failure mode this bug produced: fifty good props and one bad one exported as zero, silently.
        // The fix does not quietly drop the one bad prop and keep the other forty-nine either — a partial,
        // silently-shortened document is the same fault with fewer symptoms. It refuses the document.
        var manyGoodOneBad = """
            {"props":[
              {"kind":"tree","id":"t1","seed":1,"x":0,"z":0},
              {"kind":"tree","id":"t2","seed":2,"x":1,"z":1},
              {"kind":"boulder","id":"b1","seed":3,"x":2,"z":2},
              {"kind":"stroke","id":"p1","seed":4,"points":[[0,0],[1,1]]},
              {"kind":"flora","id":"f1","seed":5,"points":[[0,0],[1,0],[1,1]]},
              {"kind":"wtaer","id":"bad1","seed":6,"points":[[0,0],[1,1]]}
            ]}
            """.Replace("\n", "").Replace("  ", "");
        Assert.Throws<DressingParseException>(() => DressingJson.Deserialize(manyGoodOneBad));
        await Task.CompletedTask;
    }

    [Test]
    public async Task A_blob_of_json_the_reader_cannot_parse_at_all_is_refused_rather_than_dressing_nothing()
    {
        // The document is not even syntactically JSON — still refused by name rather than read as though the
        // map carried no props.
        Assert.Throws<DressingParseException>(() => DressingJson.Deserialize("{ not json"));
    }

    [Test]
    public async Task A_document_that_is_not_an_object_is_refused_rather_than_read_as_empty()
    {
        // An older shape (a bare list of props, with no `props` wrapper) is not this document's contract —
        // refusing it by name beats reading it as "nothing placed".
        var ex = Assert.Throws<DressingParseException>(() =>
            DressingJson.Deserialize("""[{"kind":"tree","id":"t1","seed":1,"x":0,"z":0}]"""));
        await Assert.That(ex.Message).Contains("list");
    }

    [Test]
    public async Task DeserializeProp_names_the_field_rather_than_returning_null()
    {
        var ex = Assert.Throws<DressingParseException>(() =>
            DressingJson.DeserializeProp("""{"kind":"stroke","id":"p1","seed":1,"points":[[0,0],[1,1]],"radius":"wide"}"""));
        await Assert.That(ex.Field).IsEqualTo("radius");
    }

    [Test]
    public async Task DeserializeProp_names_an_unknown_kind_rather_than_returning_null()
    {
        var ex = Assert.Throws<DressingParseException>(() => DressingJson.DeserializeProp("""{"kind":"unicorn"}"""));
        await Assert.That(ex.Field).IsEqualTo("kind");
        await Assert.That(ex.Message).Contains("unicorn");
    }

    [Test]
    public async Task A_well_formed_prop_still_deserializes_normally()
    {
        var prop = DressingJson.DeserializeProp("""{"kind":"tree","id":"t1","seed":1,"x":3,"z":4}""");
        await Assert.That(((TreeProp)prop).X).IsEqualTo(3);
    }

    [Test]
    public async Task Every_refusal_carries_the_export_gates_rule_id()
        => await Assert.That(DressingParseException.Rule).IsEqualTo("DR-DOC");
}
