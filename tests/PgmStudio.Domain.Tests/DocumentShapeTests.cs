using System.Text.Json.Nodes;
using PgmStudio.Domain;

namespace PgmStudio.Domain.Tests;

/// <summary>
/// Which parts of a document went unread, and — the harder half — which did not.
///
/// <para>The value of this walk is entirely in its false positives: a check that reports a field nobody
/// mistyped is worse than no check, because an author learns to ignore the warnings and then misses the real
/// one. So most of what is asserted here is silence.</para>
/// </summary>
public sealed class DocumentShapeTests
{
    private sealed record Roof(string Form, int Pitch);

    [System.Text.Json.Serialization.JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
    [System.Text.Json.Serialization.JsonDerivedType(typeof(Shed), "shed")]
    private abstract record Covering;
    private sealed record Shed(string Form) : Covering;
    private sealed record House(string Name, Roof Roof, IReadOnlyList<Roof> Wings);
    private sealed record Themed(IReadOnlyDictionary<string, Roof> Buckets);

    private sealed record Bounds(
        [property: System.Text.Json.Serialization.JsonPropertyName("min_x")] double MinX,
        [property: System.Text.Json.Serialization.JsonPropertyName("max_x")] double MaxX);

    private sealed record Dressed(System.Text.Json.JsonElement Dressing);

    private sealed record Derived(string Name)
    {
        [System.Text.Json.Serialization.JsonIgnore] public int Length => Name.Length;
    }

    /// <summary>The case the whole thing exists for: a name no property has.</summary>
    [Test]
    public async Task A_property_the_value_cannot_keep_is_named()
    {
        var node = JsonNode.Parse("""{"name":"a","rooof":{"form":"gable"}}""");

        await Assert.That(DocumentShape.Unread(node, new House("a", new Roof("gable", 45), [])))
            .IsEquivalentTo(["rooof"]);
    }

    /// <summary>A document read whole says nothing. Asserted because a walk that reported *something* on every
    /// document would pass the test above while being useless.</summary>
    [Test]
    public async Task A_document_read_whole_is_silent()
    {
        var node = JsonNode.Parse("""{"name":"a","roof":{"form":"gable","pitch":45},"wings":[]}""");

        await Assert.That(DocumentShape.Unread(node, new House("a", new Roof("gable", 45), []))).IsEmpty();
    }

    /// <summary>Nesting is reported by path, not by leaf name — a bare <c>pich</c> sends an author looking
    /// through a document that may hold several roofs.</summary>
    [Test]
    public async Task A_nested_property_is_named_by_its_path()
    {
        var node = JsonNode.Parse("""{"name":"a","roof":{"form":"gable","pich":45}}""");

        await Assert.That(DocumentShape.Unread(node, new House("a", new Roof("gable", 0), [])))
            .IsEquivalentTo(["roof.pich"]);
    }

    /// <summary>Inside a list, the index goes in the path too, because "one of your wings has a typo" is not
    /// an answer on a building with four of them.</summary>
    [Test]
    public async Task An_element_of_a_list_is_named_by_its_index()
    {
        var node = JsonNode.Parse("""{"wings":[{"form":"gable"},{"form":"hip","ptch":30}]}""");
        var value = new House("a", new Roof("gable", 0), [new Roof("gable", 0), new Roof("hip", 0)]);

        await Assert.That(DocumentShape.Unread(node, value)).IsEquivalentTo(["wings[1].ptch"]);
    }

    /// <summary>A dictionary's keys are the author's words, not the record's. A theme names its own buckets, so
    /// reading them as property names would report every bucket on every theme — the false-positive flood that
    /// would make the warning worthless.</summary>
    [Test]
    public async Task A_dictionarys_keys_are_data_and_not_field_names()
    {
        var node = JsonNode.Parse("""{"buckets":{"surface":{"form":"gable"},"rim":{"form":"hip"}}}""");
        var value = new Themed(new Dictionary<string, Roof>
        {
            ["surface"] = new("gable", 0),
            ["rim"] = new("hip", 0),
        });

        await Assert.That(DocumentShape.Unread(node, value)).IsEmpty();
    }

    /// <summary>A typo *inside* a dictionary's value is still a typo — the keys are exempt, not the objects
    /// under them.</summary>
    [Test]
    public async Task A_typo_inside_a_dictionary_value_is_still_named()
    {
        var node = JsonNode.Parse("""{"buckets":{"surface":{"form":"gable","pich":9}}}""");
        var value = new Themed(new Dictionary<string, Roof> { ["surface"] = new("gable", 0) });

        await Assert.That(DocumentShape.Unread(node, value)).IsEquivalentTo(["buckets.surface.pich"]);
    }

    /// <summary>Matching is case-insensitive, because the wire is camelCase and the record is Pascal — a walk
    /// that missed that would report every field of every document.</summary>
    [Test]
    public async Task Wire_casing_matches_the_records_casing()
    {
        var node = JsonNode.Parse("""{"Name":"a","ROOF":{"Form":"gable"}}""");

        await Assert.That(DocumentShape.Unread(node, new House("a", new Roof("gable", 0), []))).IsEmpty();
    }

    /// <summary>The polymorphic discriminator is the serializer's, not the record's — no concrete type carries
    /// a property for it, so a walk over properties alone names it on every polymorphic value in every
    /// document. That is the false positive that would have made the whole warning worthless, and it was found
    /// by posting a <em>correct</em> material and reading the answer rather than by reasoning about the walk.
    /// </summary>
    [Test]
    public async Task The_type_discriminator_is_not_an_unread_field()
    {
        var node = JsonNode.Parse("{\"kind\":\"shed\",\"form\":\"gable\"}");

        await Assert.That(DocumentShape.Unread(node, new Shed("gable"))).IsEmpty();
    }

    /// <summary>A renamed property is matched by the name the <em>document</em> uses. The three models an
    /// author writes — the plan, the sketch layout and the intent — rename most of their fields, so a walk
    /// over CLR names alone reports a correct document as almost entirely unread.</summary>
    [Test]
    public async Task A_renamed_property_is_matched_by_the_documents_name()
    {
        var node = JsonNode.Parse("""{"min_x":-10,"max_x":10}""");

        await Assert.That(DocumentShape.Unread(node, new Bounds(-10, 10))).IsEmpty();
    }

    /// <summary>And the property's own name is then <b>not</b> a name the document may use, because the
    /// serializer will not bind it either — a shape stating <c>minX</c> loses the value.</summary>
    [Test]
    public async Task The_clr_name_of_a_renamed_property_is_unread()
    {
        var node = JsonNode.Parse("""{"minX":-10,"max_x":10}""");

        await Assert.That(DocumentShape.Unread(node, new Bounds(0, 10))).IsEquivalentTo(["minX"]);
    }

    /// <summary>A property the serializer skips outright is not a place a field can land, so a document
    /// naming one is naming something nothing reads.</summary>
    [Test]
    public async Task A_field_naming_an_ignored_property_is_unread()
    {
        var node = JsonNode.Parse("""{"name":"a","length":1}""");

        await Assert.That(DocumentShape.Unread(node, new Derived("a"))).IsEquivalentTo(["length"]);
    }

    /// <summary>A member held as raw JSON keeps everything written under it, so nothing under one can be
    /// unread. Walking in compares the document against <c>JsonElement</c>'s own properties and reports every
    /// field of a document that was kept whole.</summary>
    [Test]
    public async Task Nothing_under_a_raw_json_member_is_unread()
    {
        var json = """{"dressing":{"props":[{"kind":"tree","at":[3,4]}]}}""";

        await Assert.That(DocumentShape.Unread(
            JsonNode.Parse(json), System.Text.Json.JsonSerializer.Deserialize<Dressed>(
                json, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))))
            .IsEmpty();
    }

    /// <summary>Nothing to walk against means nothing to say. A null value cannot be interrogated for property
    /// names, and guessing from the declared type is what the walk deliberately does not do.</summary>
    [Test]
    public async Task A_null_value_reports_nothing()
    {
        await Assert.That(DocumentShape.Unread(JsonNode.Parse("""{"a":1}"""), null)).IsEmpty();
    }
}
