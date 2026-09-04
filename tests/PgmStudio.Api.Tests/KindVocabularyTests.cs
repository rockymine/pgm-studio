using System.Reflection;
using System.Text.Json.Serialization;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Tests;

/// <summary>
/// A kind word is declared once, in <c>Vocabulary</c>, and every party that spells it takes it from there —
/// the type hierarchy a placement deserializes by, the gate that judges a shape, the reader that turns a
/// stated mark into a solved one, and the canvas that draws all three. What a shared constant cannot check
/// is whether each word still has a <b>reader</b>: a set may name a kind nothing builds, and the board loses
/// it in silence rather than refusing.
///
/// <para>So this asks the readers directly. A prop kind must name a derived type; a shape kind must
/// rasterize; a mark kind must produce a mark. Adding a word to a set without teaching a reader is what the
/// three below fail on.</para>
/// </summary>
public sealed class KindVocabularyTests
{
    /// <summary>Every prop kind names a subtype of <see cref="PlacedProp"/>, and the hierarchy declares no
    /// discriminator the set does not carry — the two are one list, so the refusal that names the known kinds
    /// and the picker that offers them cannot disagree.</summary>
    [Test]
    public async Task Every_prop_kind_is_a_derived_type_and_every_derived_type_is_a_prop_kind()
    {
        var declared = typeof(PlacedProp)
            .GetCustomAttributes<JsonDerivedTypeAttribute>()
            .Select(derived => (string)derived.TypeDiscriminator!)
            .ToList();

        await Assert.That(declared.Order(StringComparer.Ordinal))
            .IsEquivalentTo(PropKinds.All.Order(StringComparer.Ordinal));
    }

    /// <summary>Every prop kind answers the two questions the pass asks of one — where it goes in the order,
    /// and how far a tree stands off it — since both are read off a prototype of the kind's own type.</summary>
    [Test]
    public async Task Every_prop_kind_has_a_prototype_the_pass_can_ask()
    {
        foreach (var kind in PropKinds.All)
            await Assert.That(PlacedProp.PlacementOrderOf(kind)).IsNotNull()
                .Because($"'{kind}' is a prop kind and the pass has no order for it");
    }

    /// <summary>Every shape kind draws ground. A kind the rasterizer's switch does not name falls through to
    /// an empty ring, so the shape is in the document, contributes no cell, and nothing says so — asked here
    /// through the rasterizer's own door rather than against its switch, since what matters is the
    /// ground.</summary>
    [Test]
    public async Task Every_shape_kind_draws_ground()
    {
        foreach (var kind in ShapeKinds.All)
        {
            var layout = """
                {"layers":[{"id":"g","layout":{"shapes":[{
                  "id":"s","type":"KIND","operation":"add",
                  "min_x":-10,"min_z":-10,"max_x":10,"max_z":10,
                  "center_x":0,"center_z":0,"radius":10,
                  "vertices":[[-10,-10],[10,-10],[10,10],[-10,10]]
                }],"groups":[]}}]}
                """.Replace("KIND", kind);
            await Assert.That(SketchRasterizer.Rasterize(layout).Count).IsGreaterThan(0)
                .Because($"'{kind}' is a shape kind and the rasterizer draws no ground for it");
        }
    }

    /// <summary>Every mark kind the set names is one the reader turns into a mark. A kind it does not name
    /// reads as null and is dropped without a word, so the author's terrain is simply not on the board.</summary>
    [Test]
    public async Task Every_mark_kind_reads_as_a_mark()
    {
        foreach (var kind in MarkKinds.All)
        {
            var stated = new ReliefMarkJson
            {
                Kind = kind,
                At = [0, 0],
                Points = [[-10, 0], [10, 0]],
                Ring = [[-10, -10], [10, -10], [0, 10]],
                Heights = [12],
            };
            await Assert.That(stated.ToMark()).IsNotNull()
                .Because($"'{kind}' is a mark kind and the reader makes nothing of it");
        }
    }

    /// <summary>A push is the canvas's own word and is deliberately outside <see cref="MarkKinds.All"/>: the
    /// stored form carries no kind, so the reader answering nothing for it is the contract rather than a
    /// gap.</summary>
    [Test]
    public async Task A_push_is_not_a_mark_the_reader_answers_for()
    {
        await Assert.That(MarkKinds.All).DoesNotContain(MarkKinds.Push);
        await Assert.That(new ReliefMarkJson { Kind = MarkKinds.Push, Ring = [[0, 0], [1, 0], [0, 1]] }.ToMark())
            .IsNull();
    }
}
