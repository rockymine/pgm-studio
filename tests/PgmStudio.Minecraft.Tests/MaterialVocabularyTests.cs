using System.Text.Json;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// The vocabulary is derived rather than restated, so what is worth asserting is that the derivation holds
/// against the types it reads — a field list that has drifted from the record it describes is worse than none,
/// because a caller writes JSON from it and gets a refusal it cannot explain.
/// </summary>
public sealed class MaterialVocabularyTests
{
    [Test]
    public async Task Every_kind_the_painter_accepts_is_described()
    {
        // Build() throws either way round — a described kind the painter does not declare, or a declared kind
        // nobody described. Touching All is what runs it, so this is the assertion.
        await Assert.That(MaterialVocabulary.All.Count).IsEqualTo(14);
        await Assert.That(MaterialVocabulary.All.Select(k => k.Kind).Distinct().Count()).IsEqualTo(14);
    }

    [Test]
    public async Task A_kinds_fields_are_the_ones_its_json_actually_carries()
    {
        // The claim the whole type rests on: the names it reports are the names the serializer writes, so a
        // caller building JSON from the vocabulary is building what the painter reads. Checked against a real
        // serialized instance of each kind rather than against the reflection a second time.
        foreach (var (material, kind) in Samples())
        {
            var json = JsonDocument.Parse(TerrainThemeJson.Serialize(material)).RootElement;
            var described = MaterialVocabulary.Of(kind)!.Value;

            foreach (var field in described.Fields.Where(f => f.Required))
                await Assert.That((kind, field.Name, json.TryGetProperty(field.Name, out _)))
                    .IsEqualTo((kind, field.Name, true));

            // and nothing the JSON carries is undescribed, apart from the discriminator itself
            foreach (var written in json.EnumerateObject().Select(p => p.Name).Where(n => n != "kind"))
                await Assert.That((kind, written, described.Fields.Any(f => f.Name == written)))
                    .IsEqualTo((kind, written, true));
        }
    }

    [Test]
    public async Task An_optional_fields_stated_default_is_the_records_own()
    {
        var layered = MaterialVocabulary.Of("layered")!.Value;
        var axis = layered.Fields.Single(f => f.Name == "axis");

        await Assert.That(axis.Required).IsFalse();
        await Assert.That(axis.Default).IsEqualTo("depth");
        await Assert.That(axis.Choices).IsEquivalentTo(new[] { "depth", "inward", "height" });

        // The height axis is the one with an origin, and the origin is optional like the axis itself: a stack
        // that names no `from` reads from y0, which is what every other axis already means by "the start".
        var from = layered.Fields.Single(f => f.Name == "from");
        await Assert.That(from.Required).IsFalse();
        await Assert.That(from.Default).IsEqualTo(0);

        var frame = MaterialVocabulary.Of("wallFrame")!.Value;
        await Assert.That(frame.Fields.Single(f => f.Name == "angle").Default).IsEqualTo(45);
        await Assert.That(frame.Fields.Single(f => f.Name == "thickness").Default).IsEqualTo(1);
    }

    [Test]
    public async Task What_a_kind_reads_is_what_decides_where_it_is_legible()
    {
        // The half a field list cannot carry, and the reason a wall run came out flat: a kind reading only the
        // arc says nothing away from a perimeter, and one reading only position works anywhere.
        await Assert.That(MaterialVocabulary.Of("wallRun")!.Value.Reads).IsEquivalentTo(new[] { CellFact.Arc });
        await Assert.That(MaterialVocabulary.Of("voronoi")!.Value.Reads).IsEquivalentTo(new[] { CellFact.Position });
        await Assert.That(MaterialVocabulary.Of("layered")!.Value.Reads)
            .IsEquivalentTo(new[] { CellFact.Depth, CellFact.Inset });
        await Assert.That(MaterialVocabulary.Of("solid")!.Value.Reads).IsEmpty();
    }

    [Test]
    public async Task An_unknown_kind_is_null_rather_than_a_throw()
        => await Assert.That(MaterialVocabulary.Of("marble")).IsNull();

    /// <summary>One instance of every kind, so the field claims are checked against real serialized JSON.</summary>
    private static IEnumerable<(TerrainMaterial Material, string Kind)> Samples()
    {
        TerrainMaterial stone = new SolidMaterial(1);
        yield return (stone, "solid");
        yield return (new LayeredMaterial(new BandStack([new Band(stone, 1)])), "layered");
        yield return (new TeamTintedMaterial(159, stone), "teamTint");
        yield return (new VoronoiMaterial(1, 9, [new VoronoiBand(stone, 1)]), "voronoi");
        yield return (new CellMaterial(2, 10, 55, 4, [stone]), "cell");
        yield return (new NoiseMaterial(3, 14, 3, [stone]), "noise");
        yield return (new TurbulenceMaterial(4, 14, 3, [stone]), "turbulence");
        yield return (new ElectricMaterial(5, 16, 3, [stone]), "electric");
        yield return (new CheckerMaterial(1, stone, stone), "checker");
        yield return (new LogCheckerMaterial(1, 17), "logChecker");
        yield return (new LaidLogMaterial(17), "laidLog");
        yield return (new WallRunMaterial([new WallStripe(stone, 2)]), "wallRun");
        yield return (new WallDiagonalMaterial([new WallStripe(stone, 2)]), "wallDiagonal");
        yield return (new WallFrameMaterial(stone, stone), "wallFrame");
    }

    /// <summary>
    /// <b>What a kind reads is hand-written, so it is checked by experiment rather than trusted.</b> Every fact
    /// is varied on its own against a material built to be sensitive to it, and a kind whose output moves is a
    /// kind that reads it. Nothing may vary with a fact it does not name — an undeclared dependency is exactly
    /// what leaves a caller with a swatch or a board that changed for a reason the vocabulary never mentioned.
    ///
    /// <para>It caught one on the day it was written: <c>wallFrame</c> was described as reading the arc and the
    /// bend when it actually reads the bend, the depth and the height — which is why it is all edge seen from
    /// above, and why the elevation is its view.</para>
    /// </summary>
    [Test]
    public async Task Nothing_varies_with_a_fact_it_does_not_declare()
    {
        foreach (var (material, kind) in Sensitive())
        {
            var declared = MaterialVocabulary.Of(kind)!.Value.Reads;
            foreach (var fact in Enum.GetValues<CellFact>())
            {
                if (Resolve(material, fact, low: true) == Resolve(material, fact, low: false) ) continue;
                await Assert.That((kind, fact, declared.Contains(fact))).IsEqualTo((kind, fact, true));
            }
        }
    }

    /// <summary>The same material under one fact's two extremes, everything else held still.</summary>
    private static (int Id, int Data) Resolve(TerrainMaterial material, CellFact fact, bool low)
    {
        var ctx = new BucketContext(4, 9, 4, TerrainBucket.Surface, DepthFromTop: 2, TeamData: -1,
            PerimeterArc: 3, HeightFromBottom: 2, PerimeterTurn: 0, PerimeterRun: 0, Inset: 3);
        ctx = fact switch
        {
            CellFact.Position => low ? ctx : ctx with { X = 37, Z = 51 },
            CellFact.Depth => low ? ctx : ctx with { DepthFromTop = 0 },
            CellFact.Inset => low ? ctx : ctx with { Inset = 0 },
            CellFact.Arc => low ? ctx : ctx with { PerimeterArc = 4 },
            CellFact.Bend => low ? ctx : ctx with { PerimeterTurn = 90, PerimeterRun = 2 },
            CellFact.Height => low ? ctx : ctx with { Y = 3, HeightFromBottom = 0 },
            CellFact.Team => low ? ctx : ctx with { TeamData = 14 },
            _ => ctx,
        };
        return material.Resolve(ctx);
    }

    /// <summary>One instance per kind, each built so the facts it declares actually move it — a wall run of one
    /// stripe or a checker of one colour would pass the assertion by saying nothing.</summary>
    private static IEnumerable<(TerrainMaterial Material, string Kind)> Sensitive()
    {
        TerrainMaterial a = new SolidMaterial(1), b = new SolidMaterial(4);
        yield return (new SolidMaterial(1), "solid");
        yield return (new LayeredMaterial(new BandStack([new Band(a, 1), new Band(b, 1)])), "layered");
        yield return (new LayeredMaterial(new BandStack([new Band(a, 1), new Band(b, 1)]), BandAxis.Inward), "layered");
        yield return (new TeamTintedMaterial(159, a), "teamTint");
        yield return (new VoronoiMaterial(1, 4, [new VoronoiBand(a, 1), new VoronoiBand(b, 1)]), "voronoi");
        yield return (new CellMaterial(2, 4, 55, 4, [a, b]), "cell");
        yield return (new NoiseMaterial(3, 6, 3, [a, b]), "noise");
        yield return (new TurbulenceMaterial(4, 6, 3, [a, b]), "turbulence");
        yield return (new ElectricMaterial(5, 6, 3, [a, b]), "electric");
        yield return (new CheckerMaterial(1, a, b), "checker");
        yield return (new LogCheckerMaterial(1, 17), "logChecker");
        yield return (new LaidLogMaterial(17), "laidLog");
        yield return (new WallRunMaterial([new WallStripe(a, 1), new WallStripe(b, 1)]), "wallRun");
        yield return (new WallDiagonalMaterial([new WallStripe(a, 1), new WallStripe(b, 1)], 1), "wallDiagonal");
        yield return (new WallFrameMaterial(a, b, 45, 1), "wallFrame");
    }
}
