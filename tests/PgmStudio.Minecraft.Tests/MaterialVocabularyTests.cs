using System.Text.Json;

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
        await Assert.That(axis.Choices).IsEquivalentTo(new[] { "depth", "inward" });

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
}
