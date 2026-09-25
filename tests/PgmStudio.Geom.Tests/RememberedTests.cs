using PgmStudio.Geom;

namespace PgmStudio.Geom.Tests;

public sealed class RememberedTests
{
    [Test]
    public async Task An_unchanged_document_is_computed_once()
    {
        var remembered = new Remembered<int>(capacity: 4);
        var computations = 0;

        remembered.Of([1, 2, 3], () => ++computations);
        var answer = remembered.Of([1, 2, 3], () => ++computations);

        await Assert.That(answer).IsEqualTo(1);
        await Assert.That(computations).IsEqualTo(1);
    }

    [Test]
    public async Task An_edited_document_is_a_new_key()
    {
        var remembered = new Remembered<string>(capacity: 4);

        var first = remembered.Of([1], () => "first");
        var edited = remembered.Of([2], () => "edited");

        await Assert.That(first).IsEqualTo("first");
        await Assert.That(edited).IsEqualTo("edited");
    }

    [Test]
    public async Task Concurrent_askers_past_capacity_all_get_their_answer()
    {
        var remembered = new Remembered<int>(capacity: 2);

        var answers = await Task.WhenAll(Enumerable.Range(0, 64).Select(worker => Task.Run(() =>
        {
            var sum = 0;
            for (var round = 0; round < 2_000; round++)
            {
                var document = (byte)((worker + round) % 16);
                sum += remembered.Of([document], () => document) == document ? 1 : 0;
            }
            return sum;
        })));

        await Assert.That(answers.Sum()).IsEqualTo(64 * 2_000);
    }
}
