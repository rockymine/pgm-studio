using PgmStudio.Geom.Algorithms;
using PgmStudio.Minecraft.Dressing;

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
                new PathProp { Id = "p", Seed = 1, Points = [[0, 0], [10, 10]], Pave = new SolidMaterial(Blocks.Gravel) },
                new WaterProp { Id = "w", Seed = 2, Points = [[0, 0], [10, 10]] },
                new TreeProp { Id = "t", Seed = 3, X = 1, Z = 1 },
                new BoulderProp { Id = "b", Seed = 4, X = 2, Z = 2 },
                new FloraProp { Id = "f", Seed = 5, Points = [[0, 0], [10, 0], [10, 10]] },
                new HouseProp { Id = "h", Seed = 6, Points = [[0, 0], [5, 5]] },
            ],
        });

        var back = DressingJson.Deserialize(json);
        await Assert.That(DressingJson.Serialize(back)).IsEqualTo(json);
    }

    // ── the first-key constraint (B130) ────────────────────────────────────────────────────────────────
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
            """{"props":[{"kind":"path","id":"p1","seed":1,"points":[[0,0],[1,1]],"pave":{"id":13,"data":0,"kind":"solid"}}]}""");
        await Assert.That(((PathProp)doc.Props[0]).Pave).IsEqualTo((TerrainMaterial)new SolidMaterial(13));
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
        await Assert.That(ex.Message).Contains("path");   // one of the six known prop kinds
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
                """{"props":[{"kind":"path","id":"d9","seed":1,"points":[[0,0],[1,1]],"radius":"wide"}]}"""));

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
              {"kind":"path","id":"p1","seed":4,"points":[[0,0],[1,1]]},
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
            DressingJson.DeserializeProp("""{"kind":"path","id":"p1","seed":1,"points":[[0,0],[1,1]],"radius":"wide"}"""));
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
