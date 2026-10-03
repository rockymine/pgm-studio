using System.Text;
using PgmStudio.Api.Services;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The one store every drawn picture is kept in: a picture is drawn once per distinct input and answered from
/// its file after that, a changed input is a different picture, concurrent askers share one drawing, and a
/// picture with nothing to draw is kept as such. A picture that cannot be kept is still answered.
/// </summary>
public sealed class DrawingsTests
{
    private static string Fresh() => "drawings-test/" + Guid.NewGuid().ToString("N");

    [Test]
    public async Task A_picture_is_drawn_once_and_answered_from_its_file_after()
    {
        var drawer = Fresh();
        var draws = 0;
        byte[] Draw() { draws++; return Encoding.UTF8.GetBytes("<svg/>"); }

        var first = Drawings.Of(Drawings.Name(drawer, "row"), Draw);
        var second = Drawings.Of(Drawings.Name(drawer, "row"), Draw);

        await Assert.That(draws).IsEqualTo(1);
        await Assert.That(second).IsEquivalentTo(first!);
        await Assert.That(Drawings.TryFind(Drawings.Name(drawer, "row"), out _)).IsTrue();
    }

    [Test]
    public async Task A_changed_input_or_drawer_names_a_different_picture()
    {
        var drawer = Fresh();
        await Assert.That(Drawings.Name(drawer, "row")).IsNotEqualTo(Drawings.Name(drawer, "row edited"));
        await Assert.That(Drawings.Name(drawer, "row")).IsNotEqualTo(Drawings.Name(drawer + "/roof", "row"));
    }

    [Test]
    public async Task Nothing_to_draw_is_kept_and_answered_without_drawing_again()
    {
        var name = Drawings.Name(Fresh(), "empty");
        var draws = 0;

        await Assert.That(Drawings.Of(name, () => { draws++; return null; })).IsNull();
        await Assert.That(Drawings.Of(name, () => { draws++; return [1]; })).IsNull();
        await Assert.That(draws).IsEqualTo(1);
    }

    [Test]
    public async Task Concurrent_askers_of_one_picture_share_one_drawing()
    {
        var name = Drawings.Name(Fresh(), "busy");
        var draws = 0;
        byte[] Draw() { Interlocked.Increment(ref draws); Thread.Sleep(200); return [7]; }

        var asked = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => Drawings.Of(name, Draw))));

        await Assert.That(draws).IsEqualTo(1);
        await Assert.That(asked.All(picture => picture is [7])).IsTrue();
    }

    [Test]
    public async Task A_drawing_that_throws_is_not_kept()
    {
        var name = Drawings.Name(Fresh(), "fault");
        await Assert.That(() => Drawings.Of(name, () => throw new InvalidOperationException("no")))
            .Throws<InvalidOperationException>();
        await Assert.That(Drawings.TryFind(name, out _)).IsFalse();
    }

    [Test]
    public async Task A_picture_that_cannot_be_kept_is_still_answered()
    {
        // A directory where the picture's file would go: reading it and writing over it both fail, for this
        // name alone.
        var name = Drawings.Name(Fresh(), "unwritable");
        var blocked = Path.Combine(Drawings.Folder, name[..2], name[2..4], name);
        Directory.CreateDirectory(blocked);
        try
        {
            var draws = 0;
            byte[] Draw() { draws++; return [9]; }

            await Assert.That(Drawings.Of(name, Draw)).IsEquivalentTo(new byte[] { 9 });
            await Assert.That(Drawings.Of(name, Draw)).IsEquivalentTo(new byte[] { 9 });
            await Assert.That(draws).IsEqualTo(2);
        }
        finally { Directory.Delete(blocked, recursive: true); }
    }
}
