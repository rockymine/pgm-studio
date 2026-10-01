using PgmStudio.Analysis.Playability;
using PgmStudio.Contracts;
using PgmStudio.Geom.Relief;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Services;

/// <summary>What the relief a layout carries charges, per group — the read <c>sketch/relief/read</c> answers
/// and the report summarises, solved against the warm starts the previews keep for the map.</summary>
public static class ReliefReads
{
    /// <summary>The read of every relief group <paramref name="layoutJson"/> solves, and the findings the
    /// readings raise against what each group states about itself. Throws what the solve throws on a layout
    /// that will not read.</summary>
    public static (ReliefReadDto Read, IReadOnlyList<Finding> Complaints) Of(
        string layoutJson, long mapId, ReliefPreviewCache warm)
    {
        // What the marks did to one another, filled in as each group is solved. A seam is a fact about the
        // statements rather than about the surface, so nothing downstream of the field can recover it.
        var marks = new Dictionary<string, ReliefReading>(StringComparer.Ordinal);
        var state = SketchLayout.Parse(layoutJson);
        var fields = SketchRasterizer.ReliefFields(layoutJson,
            (group, footprint) => warm.WarmStart(mapId, group, footprint),
            (group, solved) => warm.Remember(mapId, group, solved),
            (group, reading) => marks[group] = reading);

        var mode = state?.Setup?.MirrorMode;
        var cx = state?.Setup?.Center?.Cx ?? 0;
        var cz = state?.Setup?.Center?.Cz ?? 0;

        // What each group states about itself, to read the measurement back against (RL1).
        var declared = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (group, relief) in state?.Relief ?? [])
            declared[group] = relief?.Landform;

        var complaints = new List<Finding>();
        var groups = fields.Select(entry =>
        {
            var read = ReliefReadback.Read(entry.Value, mode, cx, cz);
            complaints.AddRange(ReliefReadback.Check(read, declared.GetValueOrDefault(entry.Key), entry.Key));
            var reading = marks.GetValueOrDefault(entry.Key);
            if (reading is not null) complaints.AddRange(ReliefReadback.Check(reading, entry.Key));
            return new ReliefGroupReadDto(
                entry.Key, read.Cells, read.Low, read.High, read.Relief, read.Steps,
                [.. read.Tiers.Select(t => new ReliefTierDto(
                    t.Name, t.MaxStep, t.Share, t.Places, t.LargestPlace, t.Ledges,
                    [.. t.Parts.Select(part => new ReliefPartDto(
                        part.Cells, part.Share, part.CentroidX, part.CentroidZ,
                        part.MinX, part.MinZ, part.MaxX, part.MaxZ, part.Place))]))],
                // the whole list is long and the tail is all banks, so only the head is sent
                [.. read.Faces.Take(12).Select(f => new ReliefFaceDto(f.Facing, f.Width, f.Drop, f.Cliff))],
                read.Faces.Count, read.Cliffs,
                new ReliefFordsDto(read.AcrossX.Rows, read.AcrossX.OnFoot, read.AcrossX.WithBlock, read.AcrossX.Descended),
                new ReliefFordsDto(read.AcrossZ.Rows, read.AcrossZ.OnFoot, read.AcrossZ.WithBlock, read.AcrossZ.Descended),
                // A group with no barrier divides by nothing, and infinity is not a JSON number.
                read.SymmetryError, read.Landform,
                double.IsInfinity(read.Smoothing) ? null : read.Smoothing,
                read.Level, read.LargestField,
                [.. (reading?.Seams ?? []).Take(12)
                        .Select(seam => new ReliefSeamDto(seam.A, seam.B, seam.Step, seam.X, seam.Z, seam.Cells))],
                reading?.Silent ?? [],
                [.. (reading?.Pushes ?? []).Select(push =>
                        new ReliefPushGradeDto(push.Id, Math.Round(push.Skirt, 2),
                                               Math.Round(push.Crown, 2), push.Cells))]);
        }).ToList();

        return (new ReliefReadDto(groups), complaints);
    }
}
