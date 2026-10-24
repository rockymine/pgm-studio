using System.Reflection;

namespace PgmStudio.Vocabulary.Tests;

public sealed class ModifierWordsTests
{
    private static IEnumerable<string> Constants(Type words) =>
        words.GetFields(BindingFlags.Public | BindingFlags.Static)
             .Where(field => field.IsLiteral && field.FieldType == typeof(string))
             .Select(field => (string)field.GetRawConstantValue()!);

    [Test]
    public async Task Every_operation_the_vocabulary_names_is_listed_once_and_labelled_distinctly()
    {
        await Assert.That(ModifierOps.All.Order()).IsEquivalentTo(Constants(typeof(ModifierOps)).Order());
        await Assert.That(ModifierOps.All.Distinct().Count()).IsEqualTo(ModifierOps.All.Length);
        await Assert.That(ModifierOps.All.Select(ModifierOps.Label).Distinct().Count()).IsEqualTo(ModifierOps.All.Length);
        await Assert.That(ModifierOps.All.All(op => ModifierOps.Label(op) != op)).IsTrue();
    }

    [Test]
    public async Task Every_mask_kind_the_vocabulary_names_is_listed_once()
    {
        await Assert.That(MaskKinds.All.Order()).IsEquivalentTo(Constants(typeof(MaskKinds)).Order());
        await Assert.That(MaskKinds.All).Contains(MaskKinds.Line);
    }

    [Test]
    public async Task An_unknown_operation_is_labelled_as_it_was_written()
    {
        await Assert.That(ModifierOps.Label("bogus")).IsEqualTo("bogus");
        await Assert.That(ModifierOps.Label(null)).IsEqualTo("");
    }
}
