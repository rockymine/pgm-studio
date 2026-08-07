using System.Text.Json;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// <see cref="IntentCarry.CarryAuthored"/> — what survives replacing a map's intent with a freshly compiled
/// one. The plan owns the structure and is meant to overwrite it; the map's authors are the author's, the
/// compiler never fills them, and a plain replace wiped the credits on every rebuild. The two slices that
/// are <em>not</em> carried matter as much, so they are asserted too.
/// </summary>
public sealed class IntentCarryTests
{
    // What the compiler emits: the plan's structure, its own name, the credits left empty, and an
    // islandTeams the compile endpoint pre-fills from the geometry it just built.
    private const string Compiled = """
    {"teams":[{"id":"red","name":"Red","color":"red"}],
     "spawns":[{"team":"red","point":{"x":1,"y":2,"z":3}}],
     "meta":{"name":"Planned Name","authors":[],"contributors":[]},
     "islandTeams":{"1":"blue","2":"red"},
     "structures":{"walls":[{"minX":0,"minZ":0,"maxX":2,"maxZ":10,"topY":11}]}}
    """;

    // What a map that has been through Configure holds.
    private const string Stored = """
    {"teams":[{"id":"red","name":"Old","color":"red"}],
     "spawns":[{"team":"red","point":{"x":9,"y":9,"z":9}}],
     "meta":{"name":"Old Name","authors":[{"name":"rockymine","contribution":"Drawing"}],
             "contributors":[{"name":"Ruediger_LP","contribution":"Wisdom"}]},
     "islandTeams":{"1":"red","2":"blue"},
     "symmetry":{"mode":"rot_180","centerX":0,"centerZ":0}}
    """;

    private static JsonElement Merge(string compiled, string? stored)
        => JsonDocument.Parse(IntentCarry.CarryAuthored(compiled, stored)).RootElement;

    [Test]
    public async Task The_credits_survive_a_rebuild()
    {
        var meta = Merge(Compiled, Stored).GetProperty("meta");
        await Assert.That(meta.GetProperty("authors")[0].GetProperty("name").GetString()).IsEqualTo("rockymine");
        await Assert.That(meta.GetProperty("contributors")[0].GetProperty("name").GetString()).IsEqualTo("Ruediger_LP");
    }

    [Test]
    public async Task The_structure_comes_from_the_plan_including_the_name()
    {
        var merged = Merge(Compiled, Stored);
        await Assert.That(merged.GetProperty("teams")[0].GetProperty("name").GetString()).IsEqualTo("Red");
        await Assert.That(merged.GetProperty("spawns")[0].GetProperty("point").GetProperty("x").GetInt32()).IsEqualTo(1);
        // The plan owns the map's identity — renaming a plan is how a built map gets renamed.
        await Assert.That(merged.GetProperty("meta").GetProperty("name").GetString()).IsEqualTo("Planned Name");
        // And the directives the rebuild exists to deliver are untouched.
        await Assert.That(merged.GetProperty("structures").GetProperty("walls").GetArrayLength()).IsEqualTo(1);
    }

    [Test]
    public async Task Island_ownership_comes_from_the_board_that_was_just_built()
    {
        // Not carried, and not an oversight: island ids are positional over the footprint, so a stored
        // assignment made about the old board may name a different island on the new one. The compile
        // endpoint's pre-fill is derived from the geometry being built, which is the only coherent answer.
        var merged = Merge(Compiled, Stored);
        await Assert.That(merged.GetProperty("islandTeams").GetProperty("1").GetString()).IsEqualTo("blue");
    }

    [Test]
    public async Task A_compiled_intent_never_gains_a_symmetry()
    {
        // Also not carried: a compiled intent is already fanned across the orbit, and setting this field
        // turns SymmetryExpander on — which rebuilds the intent from a fixed property set and would drop the
        // structures, destroyables and cores with it.
        var merged = Merge(Compiled, Stored);
        await Assert.That(merged.TryGetProperty("symmetry", out _)).IsFalse();
    }

    [Test]
    public async Task An_empty_list_counts_as_nothing_said_not_as_a_stated_emptiness()
    {
        // The compiler writes "authors": [] rather than omitting the key, so presence alone cannot decide
        // whether it has an answer. Testing for emptiness is what makes the carry fire at all.
        var merged = Merge(Compiled, Stored);
        await Assert.That(merged.GetProperty("meta").GetProperty("authors").GetArrayLength()).IsEqualTo(1);
    }

    [Test]
    public async Task A_compiled_intent_that_does_name_an_author_keeps_its_own()
    {
        var merged = Merge("""{"meta":{"name":"N","authors":[{"name":"someone"}]}}""", Stored);
        await Assert.That(merged.GetProperty("meta").GetProperty("authors")[0].GetProperty("name").GetString()).IsEqualTo("someone");
        await Assert.That(merged.GetProperty("meta").GetProperty("contributors").GetArrayLength()).IsEqualTo(1);   // this one still rides
    }

    [Test]
    public async Task A_first_build_and_an_unreadable_store_both_pass_the_compile_through()
    {
        await Assert.That(IntentCarry.CarryAuthored(Compiled, null)).IsEqualTo(Compiled);
        await Assert.That(IntentCarry.CarryAuthored(Compiled, "")).IsEqualTo(Compiled);
        await Assert.That(IntentCarry.CarryAuthored(Compiled, "{}")).IsEqualTo(Compiled);
        await Assert.That(IntentCarry.CarryAuthored(Compiled, "not json")).IsEqualTo(Compiled);
    }
}
