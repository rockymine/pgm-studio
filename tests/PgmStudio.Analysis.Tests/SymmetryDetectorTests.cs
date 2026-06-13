using PgmStudio.Analysis;

namespace PgmStudio.Analysis.Tests;

/// <summary>
/// B7 symmetry-detection tests. Expected values are the reference `detect_from_data`
/// (symmetry/detection.py) output for the same synthetic islands — genuine parity on a controlled
/// case. Broad corpus parity (port endpoint vs reference) was also verified manually.
/// </summary>
public sealed class SymmetryDetectorTests
{
    // Two equal squares mirrored across x=5 → centre (5,1); mirror_x / mirror_z / rot_180 all hold.
    private static readonly SymmetryDetector.Island[] MirroredSquares =
    [
        new(1, 4, 1, 1, [(0, 0), (0, 2), (2, 2), (2, 0), (0, 0)], [0, 0, 2, 2]),
        new(2, 4, 9, 1, [(8, 0), (8, 2), (10, 2), (10, 0), (8, 0)], [8, 0, 10, 2]),
    ];

    [Test]
    public async Task Detects_mirror_and_rot180_on_mirrored_squares()
    {
        var r = SymmetryDetector.Detect(MirroredSquares);
        await Assert.That(r.Cx).IsEqualTo(5.0);
        await Assert.That(r.Cz).IsEqualTo(1.0);

        bool Det(string t) => r.Modes.First(m => m.Type == t).Detected;
        double Conf(string t) => r.Modes.First(m => m.Type == t).Confidence;

        await Assert.That(Det("mirror_x")).IsTrue();
        await Assert.That(Conf("mirror_x")).IsEqualTo(1.0);
        await Assert.That(Det("mirror_z")).IsTrue();
        await Assert.That(Det("rot_180")).IsTrue();
        await Assert.That(Det("rot_90")).IsFalse();
        await Assert.That(Det("mirror_d1")).IsFalse();
        await Assert.That(Conf("mirror_d1")).IsEqualTo(0.0);
    }

    [Test]
    public async Task No_islands_reports_all_modes_undetected()
    {
        var r = SymmetryDetector.Detect([]);
        await Assert.That(r.Modes.Count).IsEqualTo(6);
        await Assert.That(r.Modes.All(m => !m.Detected)).IsTrue();
        await Assert.That(r.Cx).IsEqualTo(0.0);
    }
}
