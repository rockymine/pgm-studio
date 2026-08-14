using System.Text.Json;
using PgmStudio.Pgm;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// <see cref="DocumentOverlay.Merge"/> — the mechanism behind a spec's addressing layer over a real
/// document (`docs/tools/mapgen-review.md` MG29): an overlay states additions in the target document's own
/// vocabulary, and nothing here may cause it to lose what the target already held.
/// </summary>
public sealed class DocumentOverlayTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Test]
    public async Task An_array_the_overlay_also_names_is_appended_to_not_replaced()
    {
        // A naive "the overlay's value wins" merge (JsonSerializer.Deserialize<T>(overlay) written over the
        // target verbatim, or a plain dictionary-indexer replace) would leave only the overlay's one shape —
        // exactly the loss an addressing layer exists to prevent, since the base document is what a plan
        // compile or a compose call already produced.
        var target = """{"shapes":[{"id":"s0"},{"id":"s1"}]}""";
        var overlay = """{"shapes":[{"id":"s2"}]}""";

        var merged = Parse(DocumentOverlay.Merge(target, overlay));
        var ids = merged.GetProperty("shapes").EnumerateArray()
            .Select(shape => shape.GetProperty("id").GetString()!).ToList();

        await Assert.That(ids).IsEquivalentTo(["s0", "s1", "s2"]);
    }

    [Test]
    public async Task An_object_the_overlay_also_names_merges_key_by_key_rather_than_replacing()
    {
        // The same failure mode as the array case, one level down: a theme registry is an object keyed by
        // theme id, and an overlay naming one more theme must not erase the ones the base already carried.
        var target = """{"themes":{"map":{"surface":"grass"},"steps":{"surface":"stone"}}}""";
        var overlay = """{"themes":{"steps":{"surface":"brick"},"rim":{"surface":"sand"}}}""";

        var merged = Parse(DocumentOverlay.Merge(target, overlay)).GetProperty("themes");

        await Assert.That(merged.GetProperty("map").GetProperty("surface").GetString()).IsEqualTo("grass");
        await Assert.That(merged.GetProperty("steps").GetProperty("surface").GetString()).IsEqualTo("brick");
        await Assert.That(merged.GetProperty("rim").GetProperty("surface").GetString()).IsEqualTo("sand");
    }

    [Test]
    public async Task A_scalar_the_overlay_also_names_is_replaced()
    {
        var target = """{"mapTheme":"grass","maxPlayers":12}""";
        var overlay = """{"mapTheme":"steps"}""";

        var merged = Parse(DocumentOverlay.Merge(target, overlay));

        await Assert.That(merged.GetProperty("mapTheme").GetString()).IsEqualTo("steps");
        await Assert.That(merged.GetProperty("maxPlayers").GetInt32()).IsEqualTo(12);
    }

    [Test]
    public async Task An_explicit_null_in_the_overlay_clears_the_target_key()
    {
        var target = """{"observer":{"x":0,"z":0}}""";
        var overlay = """{"observer":null}""";

        var merged = Parse(DocumentOverlay.Merge(target, overlay));

        await Assert.That(merged.GetProperty("observer").ValueKind).IsEqualTo(JsonValueKind.Null);
    }

    [Test]
    public async Task A_key_the_target_never_had_is_added_verbatim()
    {
        var target = """{"shapes":[]}""";
        var overlay = """{"dressing":{"props":[{"kind":"tree","x":4,"z":9}]}}""";

        var merged = Parse(DocumentOverlay.Merge(target, overlay));

        await Assert.That(merged.GetProperty("dressing").GetProperty("props").GetArrayLength()).IsEqualTo(1);
    }

    [Test]
    public async Task Nested_merges_recurse_more_than_one_level()
    {
        var target = """{"relief":{"north":{"base":6,"marks":[{"kind":"point"}]}}}""";
        var overlay = """{"relief":{"north":{"marks":[{"kind":"rim"}]},"south":{"base":4}}}""";

        var merged = Parse(DocumentOverlay.Merge(target, overlay)).GetProperty("relief");

        await Assert.That(merged.GetProperty("north").GetProperty("base").GetInt32()).IsEqualTo(6);
        await Assert.That(merged.GetProperty("north").GetProperty("marks").GetArrayLength()).IsEqualTo(2);
        await Assert.That(merged.GetProperty("south").GetProperty("base").GetInt32()).IsEqualTo(4);
    }

    [Test]
    public async Task Merging_leaves_the_document_valid_JSON_with_no_stray_state()
    {
        // The invariant a round trip has to hold regardless of shape: what comes out parses, and every top-
        // level key from both sides is present exactly once.
        var target = """{"a":1,"b":[1]}""";
        var overlay = """{"b":[2],"c":3}""";

        var merged = Parse(DocumentOverlay.Merge(target, overlay));

        await Assert.That(merged.EnumerateObject().Select(p => p.Name)).IsEquivalentTo(["a", "b", "c"]);
    }
}
