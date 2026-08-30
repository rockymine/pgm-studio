using PgmStudio.Geom;
using PgmStudio.Geom.Algorithms;
using PgmStudio.Minecraft.Dressing;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// The lobes a boulder is filled from (docs/world-export/decoration.md §5). A boulder is a glacial erratic —
/// a mass carried here and left standing on the ground — so what these assert is the proportion that reads as
/// weight: the bulk over the surface, a broad foot at it, a rock taller than a step, and a silhouette that is
/// this rock's rather than the form's.
/// </summary>
public sealed class BoulderShapesTests
{
    /// <summary>The blocks a rock of this form and size fills, in its own frame with the ground at y = 0.</summary>
    private static HashSet<(int X, int Y, int Z)> Rock(BoulderForm form, double size, uint seed)
    {
        var lobes = BoulderShapes.Of(form, size, seed);
        var (min, max) = Blob.Bounds(lobes);
        var cells = new HashSet<(int X, int Y, int Z)>();
        for (var y = (int)Math.Floor(min.Y); y <= (int)Math.Ceiling(max.Y); y++)
        for (var z = (int)Math.Floor(min.Z); z <= (int)Math.Ceiling(max.Z); z++)
        for (var x = (int)Math.Floor(min.X); x <= (int)Math.Ceiling(max.X); x++)
            if (Blob.Contains(lobes, new Vec3(x, y, z), seed)) cells.Add((x, y, z));
        return cells;
    }

    [Test]
    public async Task An_erratic_stands_on_the_ground_and_is_only_bedded_into_it()
    {
        foreach (var form in new[] { BoulderForm.Round, BoulderForm.Angular })
        foreach (var size in new[] { 2.0, 4.0, 7.0, 10.0 })
        {
            var rock = Rock(form, size, seed: 11);
            var below = rock.Count(cell => cell.Y < 0);

            // Most of the rock is over the surface: a mass a glacier left, not a knuckle of bedrock emerging.
            await Assert.That(below / (double)rock.Count).IsLessThan(0.30);
            await Assert.That(below).IsGreaterThan(0);   // and bedded, so no course shows daylight under it
        }
    }

    [Test]
    public async Task An_erratic_carries_its_weight_low_and_stands_taller_than_it_is_bedded()
    {
        foreach (var size in new[] { 4.0, 7.0, 10.0 })
        {
            var rock = Rock(BoulderForm.Round, size, seed: 11);
            var stands = rock.Max(cell => cell.Y) + 1;
            var wide = rock.Max(cell => cell.X) - rock.Min(cell => cell.X) + 1;

            // Tall enough to take cover behind rather than step over, and never a pole.
            await Assert.That(stands).IsGreaterThan((int)size);
            await Assert.That(stands / (double)wide).IsGreaterThan(0.4);
            await Assert.That(stands / (double)wide).IsLessThan(1.0);

            // The foot is nearly the rock's whole width, which is what reads as weight rather than balance.
            var foot = rock.Where(cell => cell.Y == 0).ToList();
            await Assert.That(foot.Max(cell => cell.X) - foot.Min(cell => cell.X) + 1)
                .IsGreaterThan((int)(wide * 0.75));

            // And its bulk is in the lower half of what stands above the ground.
            var lower = rock.Count(cell => cell.Y >= 0 && cell.Y < stands / 2.0);
            await Assert.That(lower / (double)rock.Count(cell => cell.Y >= 0)).IsGreaterThan(0.5);
        }
    }

    [Test]
    public async Task Two_rocks_of_one_form_and_size_are_not_the_same_rock()
    {
        // The lobes are thrown out on bearings hashed from the rock's own seed, so a scatter of erratics is a
        // scatter of shapes rather than one shape stamped over and over.
        var first = Rock(BoulderForm.Round, 7, seed: 11);
        var again = Rock(BoulderForm.Round, 7, seed: 11);
        var other = Rock(BoulderForm.Round, 7, seed: 23);

        await Assert.That(first.SetEquals(again)).IsTrue();          // and a seed always builds the same rock
        await Assert.That(first.SetEquals(other)).IsFalse();
        var differing = first.Union(other).Count(cell => first.Contains(cell) != other.Contains(cell));
        await Assert.That(differing / (double)first.Count).IsGreaterThan(0.1);
    }

    [Test]
    public async Task An_outcrop_is_the_one_form_that_emerges_from_the_ground()
    {
        // An outcrop is bedrock showing through, so its middle stays at the surface where an erratic's is
        // lifted clear of it — and it is far wider than it is tall either way.
        var outcrop = Rock(BoulderForm.Outcrop, 7, seed: 11);
        var erratic = Rock(BoulderForm.Round, 7, seed: 11);

        await Assert.That(outcrop.Count(cell => cell.Y < 0) / (double)outcrop.Count)
            .IsGreaterThan(erratic.Count(cell => cell.Y < 0) / (double)erratic.Count);
        var wide = outcrop.Max(cell => cell.X) - outcrop.Min(cell => cell.X) + 1;
        await Assert.That((outcrop.Max(cell => cell.Y) + 1) / (double)wide).IsLessThan(0.35);
    }

    [Test]
    public async Task A_rock_is_one_body_and_no_block_of_it_hangs_in_the_air()
    {
        // Erosion breaks the rock's outline; it may not break the rock. A block joined to the mass at a corner
        // alone has air on all six faces and is seen through as a chip floating beside the boulder.
        static IEnumerable<(int X, int Y, int Z)> Faces((int X, int Y, int Z) cell)
        {
            yield return (cell.X + 1, cell.Y, cell.Z);
            yield return (cell.X - 1, cell.Y, cell.Z);
            yield return (cell.X, cell.Y + 1, cell.Z);
            yield return (cell.X, cell.Y - 1, cell.Z);
            yield return (cell.X, cell.Y, cell.Z + 1);
            yield return (cell.X, cell.Y, cell.Z - 1);
        }

        foreach (var form in Enum.GetValues<BoulderForm>())
        foreach (var size in new[] { 2.0, 4.0, 7.0, 10.0 })
        for (uint seed = 1; seed <= 6; seed++)
        {
            var rock = Rock(form, size, seed);
            await Assert.That(rock.Count(cell => !Faces(cell).Any(rock.Contains))).IsEqualTo(0);

            var pending = new HashSet<(int X, int Y, int Z)>(rock);
            var pieces = 0;
            while (pending.Count > 0)
            {
                var start = pending.First();
                pending.Remove(start);
                var queue = new Queue<(int X, int Y, int Z)>([start]);
                while (queue.Count > 0)
                    foreach (var next in Faces(queue.Dequeue()))
                        if (pending.Remove(next)) queue.Enqueue(next);
                pieces++;
            }
            await Assert.That(pieces).IsEqualTo(1);
        }
    }
}
