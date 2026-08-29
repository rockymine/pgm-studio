using System.Text.Json;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// <see cref="SketchLayout.CarryFinish"/> — what survives rebuilding a map from its plan. A compiled layout
/// states the board and nothing else, so the finish keys the sketch accumulates (themes, the map default,
/// the bound room shells, placed dressing, the biome the ground is tinted by) have to be carried onto it or a
/// recompile strips a finished map back to bare stone.
/// </summary>
public sealed class SketchLayoutFinishTests
{
    private const string Compiled = """
    {"setup":{"mirror_mode":"rot_180"},"layers":[{"id":"L","layout":{"shapes":[],"islands":[]}}]}
    """;

    private const string Finished = """
    {"setup":{"mirror_mode":"mirror_x"},
     "themes":{"grass":{"surface":"grass"}},"mapTheme":"grass",
     "roomStyles":{"cage":{"walls":"quartz"}},
     "dressing":{"props":[{"kind":"tree","x":4,"z":9}]},
     "biome":{"kind":"cell","seed":3,"cellSize":2,"jitter":70,"palette":[4,7]},
     "layers":[{"id":"old","layout":{"shapes":[{"id":"s0"}],"islands":[]}}]}
    """;

    private static JsonElement Merge(string compiled, string? stored)
        => JsonDocument.Parse(SketchLayout.CarryFinish(compiled, stored)).RootElement;

    [Test]
    public async Task Every_finish_key_rides_across_a_recompile()
    {
        var merged = Merge(Compiled, Finished);
        foreach (var key in SketchLayout.FinishKeys)
            await Assert.That(merged.TryGetProperty(key, out _)).IsTrue();
        await Assert.That(merged.GetProperty("mapTheme").GetString()).IsEqualTo("grass");
        await Assert.That(merged.GetProperty("dressing").GetProperty("props").GetArrayLength()).IsEqualTo(1);
        await Assert.That(merged.GetProperty("biome").GetProperty("kind").GetString()).IsEqualTo("cell");
    }

    [Test]
    public async Task Geometry_and_frame_come_from_the_compile_not_the_stored_layout()
    {
        var merged = Merge(Compiled, Finished);
        await Assert.That(merged.GetProperty("setup").GetProperty("mirror_mode").GetString()).IsEqualTo("rot_180");
        await Assert.That(merged.GetProperty("layers").GetArrayLength()).IsEqualTo(1);
        await Assert.That(merged.GetProperty("layers")[0].GetProperty("id").GetString()).IsEqualTo("L");
    }

    [Test]
    public async Task A_map_with_no_stored_layout_keeps_the_compiled_one_verbatim()
    {
        await Assert.That(SketchLayout.CarryFinish(Compiled, null)).IsEqualTo(Compiled);
        await Assert.That(SketchLayout.CarryFinish(Compiled, "")).IsEqualTo(Compiled);
        await Assert.That(SketchLayout.CarryFinish(Compiled, "{}")).IsEqualTo(Compiled);
    }

    [Test]
    public async Task An_unreadable_stored_layout_never_blocks_the_rebuild()
        => await Assert.That(SketchLayout.CarryFinish(Compiled, "not json at all")).IsEqualTo(Compiled);

    [Test]
    public async Task A_null_finish_key_on_the_compiled_layout_reads_as_absent()
    {
        // What the compile endpoint actually sends: the layout is serialized from the typed model, which
        // writes every property, so the four finish keys arrive spelled out as null. Reading them as "already
        // decided" would carry nothing at all and quietly restore the bug this guards.
        var merged = Merge(
            """{"setup":{},"themes":null,"mapTheme":null,"roomStyles":null,"dressing":null,"layers":[]}""",
            Finished);
        await Assert.That(merged.GetProperty("mapTheme").GetString()).IsEqualTo("grass");
        await Assert.That(merged.GetProperty("dressing").GetProperty("props").GetArrayLength()).IsEqualTo(1);
        await Assert.That(merged.GetProperty("roomStyles").ValueKind).IsNotEqualTo(JsonValueKind.Null);
    }

    [Test]
    public async Task A_compiled_layout_that_already_states_a_finish_key_keeps_its_own()
    {
        var merged = Merge("""{"mapTheme":"desert","layers":[]}""", Finished);
        await Assert.That(merged.GetProperty("mapTheme").GetString()).IsEqualTo("desert");
        await Assert.That(merged.TryGetProperty("dressing", out _)).IsTrue();   // the rest still ride
    }
}
