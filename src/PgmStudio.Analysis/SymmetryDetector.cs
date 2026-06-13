using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Utilities;

namespace PgmStudio.Analysis;

/// <summary>
/// Global symmetry detection over island polygons (B7, port of <c>symmetry/detection.py</c>).
/// Pairs equal-area islands and checks which transforms (about the map centre) map them onto each
/// other, then verifies each candidate with a polygon IoU (NetTopologySuite). Confidence blends
/// geometric pair-support (0.4) and IoU (0.6); a mode is "detected" at confidence ≥ 0.60.
/// </summary>
public static class SymmetryDetector
{
    /// <summary>An island as consumed by detection: area + centre (from bounds) + exterior ring.</summary>
    public sealed record Island(int Id, int Area, double Cx, double Cz, IReadOnlyList<(double x, double z)> Exterior, double[] Bounds);

    public sealed record Mode(string Type, bool Detected, double Confidence);
    public sealed record Result(IReadOnlyList<Mode> Modes, double Cx, double Cz);

    private static readonly string[] Candidates = ["mirror_x", "mirror_z", "mirror_d1", "mirror_d2", "rot_180", "rot_90"];
    private static readonly string[] PairTransforms = ["mirror_x", "mirror_z", "mirror_d1", "mirror_d2", "rot_180", "rot_90", "rot_270"];
    private static readonly Dictionary<string, (double nx, double nz)> ReflectionNormals = new()
    {
        ["mirror_x"] = (1.0, 0.0), ["mirror_z"] = (0.0, 1.0), ["mirror_d1"] = (1.0, -1.0), ["mirror_d2"] = (1.0, 1.0),
    };
    private static readonly Dictionary<string, int> RotationDeg = new() { ["rot_180"] = 180, ["rot_90"] = 90, ["rot_270"] = 270 };
    private const double GroupIouThreshold = 0.85;

    private static readonly GeometryFactory Gf = new();

    /// <summary>Detect symmetry from islands (already excluding observer/excluded islands).</summary>
    public static Result Detect(IReadOnlyList<Island> islands)
    {
        if (islands.Count == 0)
            return new Result(Candidates.Select(t => new Mode(t, false, 0.0)).ToArray(), 0.0, 0.0);

        double minX = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxZ = double.MinValue;
        foreach (var i in islands)
        {
            if (i.Bounds[0] < minX) minX = i.Bounds[0];
            if (i.Bounds[1] < minZ) minZ = i.Bounds[1];
            if (i.Bounds[2] > maxX) maxX = i.Bounds[2];
            if (i.Bounds[3] > maxZ) maxZ = i.Bounds[3];
        }
        var cx = (minX + maxX) / 2.0;
        var cz = (minZ + maxZ) / 2.0;

        var (pairs, counts, totalPairs) = AggregatePairTransforms(islands, cx, cz);
        var ious = Candidates.ToDictionary(t => t, t => VerifyPolygonSymmetry(islands, cx, cz, t));

        var modes = new List<Mode>();
        foreach (var symType in Candidates)
        {
            var iou = ious[symType];
            double pairSupport;
            if (symType == "rot_90")
            {
                if (counts.GetValueOrDefault("rot_90") == 0 || counts.GetValueOrDefault("rot_270") == 0)
                    pairSupport = 0.0;
                else
                {
                    var compatible = new HashSet<string> { "rot_90", "rot_270" };
                    if (ious.GetValueOrDefault("rot_180") >= GroupIouThreshold) compatible.Add("rot_180");
                    if (ious.GetValueOrDefault("mirror_x") >= GroupIouThreshold && ious.GetValueOrDefault("mirror_z") >= GroupIouThreshold)
                    { compatible.Add("mirror_x"); compatible.Add("mirror_z"); }
                    var supporting = pairs.Count(p => p.Transforms.Any(compatible.Contains));
                    pairSupport = totalPairs > 0 ? (double)supporting / totalPairs : 0.0;
                }
            }
            else
            {
                var (sup, tot) = GeometricPairSupport(islands, symType, cx, cz);
                pairSupport = tot > 0 ? (double)sup / tot : 0.0;
            }

            var confidence = totalPairs > 0 ? 0.4 * pairSupport + 0.6 * iou : iou;
            confidence = Math.Round(confidence, 3, MidpointRounding.ToEven);
            modes.Add(new Mode(symType, confidence >= 0.60, confidence));
        }

        return new Result(modes, cx, cz);
    }

    // ── pairing ──────────────────────────────────────────────────────────────────
    private sealed record PairDetail(int IslandA, int IslandB, int Area, List<string> Transforms);

    private static (List<PairDetail> pairs, Dictionary<string, int> counts, int total)
        AggregatePairTransforms(IReadOnlyList<Island> islands, double cx, double cz, double tolerance = 3.0)
    {
        var byArea = islands.GroupBy(i => i.Area);
        var canonical = new List<(Island a, Island b)>();
        foreach (var group in byArea)
        {
            var g = group.ToList();
            if (g.Count == 2) canonical.Add((g[0], g[1]));
            else if (g.Count >= 4)
                for (var i = 0; i < g.Count; i++)
                    for (var j = i + 1; j < g.Count; j++)
                        canonical.Add((g[i], g[j]));
        }

        var counts = new Dictionary<string, int>();
        var details = new List<PairDetail>();
        foreach (var (a, b) in canonical)
        {
            var transforms = DetectPairTransform(a, b, cx, cz, tolerance);
            foreach (var t in transforms) counts[t] = counts.GetValueOrDefault(t) + 1;
            details.Add(new PairDetail(a.Id, b.Id, a.Area, transforms));
        }
        return (details, counts, canonical.Count);
    }

    private static List<string> DetectPairTransform(Island a, Island b, double cx, double cz, double tolerance)
    {
        var outList = new List<string>();
        foreach (var mode in PairTransforms)
        {
            var (ex, ez) = ApplyTransform(a.Cx, a.Cz, mode, cx, cz);
            if (Math.Abs(ex - b.Cx) < tolerance && Math.Abs(ez - b.Cz) < tolerance) outList.Add(mode);
        }
        return outList;
    }

    // ── geometric pair support (handles groups of 4+ identical-area islands) ───────
    private static (int supporting, int total) GeometricPairSupport(IReadOnlyList<Island> islands, string transform, double cx, double cz, double tolerance = 3.0)
    {
        int supporting = 0, total = 0;
        foreach (var group in islands.GroupBy(i => i.Area))
        {
            var selfSym = new List<Island>();
            var needsPartner = new List<Island>();
            foreach (var isl in group)
            {
                var (ex, ez) = ApplyTransform(isl.Cx, isl.Cz, transform, cx, cz);
                var dist = Math.Sqrt((ex - isl.Cx) * (ex - isl.Cx) + (ez - isl.Cz) * (ez - isl.Cz));
                if (dist < tolerance) selfSym.Add(isl); else needsPartner.Add(isl);
            }

            supporting += selfSym.Count;
            total += selfSym.Count;

            var n = needsPartner.Count;
            if (n == 0) continue;
            if (n == 1) { total += 1; continue; }

            total += n / 2;
            if (n % 2 == 1) total += 1;

            var unassigned = Enumerable.Range(0, n).ToList();
            var paired = 0;
            while (unassigned.Count >= 2)
            {
                var i = unassigned[0];
                var (ex, ez) = ApplyTransform(needsPartner[i].Cx, needsPartner[i].Cz, transform, cx, cz);
                int? bestJ = null;
                var bestDist = double.PositiveInfinity;
                foreach (var j in unassigned.Skip(1))
                {
                    var d = Math.Sqrt((ex - needsPartner[j].Cx) * (ex - needsPartner[j].Cx) + (ez - needsPartner[j].Cz) * (ez - needsPartner[j].Cz));
                    if (d < bestDist) { bestDist = d; bestJ = j; }
                }
                unassigned.Remove(i);
                if (bestJ is { } bj && bestDist < tolerance) { unassigned.Remove(bj); paired++; }
            }
            supporting += paired;
        }
        return (supporting, total);
    }

    // ── polygon IoU verification ───────────────────────────────────────────────────
    private static double VerifyPolygonSymmetry(IReadOnlyList<Island> islands, double cx, double cz, string transform)
    {
        var original = new List<Geometry>();
        var transformed = new List<Geometry>();
        foreach (var isl in islands)
        {
            if (isl.Exterior.Count < 3) continue;
            var orig = MakePolygon(isl.Exterior);
            if (orig is null || orig.IsEmpty) continue;
            original.Add(orig);

            var tCoords = isl.Exterior.Select(p => ApplyTransform(p.x, p.z, transform, cx, cz)).ToList();
            var trans = MakePolygon(tCoords);
            if (trans is not null && !trans.IsEmpty) transformed.Add(trans);
        }
        if (original.Count == 0 || transformed.Count == 0) return 0.0;

        var origUnion = Gf.BuildGeometry(original).Union();
        var transUnion = Gf.BuildGeometry(transformed).Union();
        var unionArea = origUnion.Union(transUnion).Area;
        if (unionArea < 1e-6) return 0.0;
        return origUnion.Intersection(transUnion).Area / unionArea;
    }

    private static Polygon? MakePolygon(IReadOnlyList<(double x, double z)> ext)
    {
        if (ext.Count < 3) return null;
        var coords = new List<Coordinate>(ext.Count + 1);
        foreach (var (x, z) in ext) coords.Add(new Coordinate(x, z));
        if (!coords[0].Equals2D(coords[^1])) coords.Add(new Coordinate(coords[0].X, coords[0].Y));
        if (coords.Count < 4) return null;
        try
        {
            var poly = Gf.CreatePolygon(coords.ToArray());
            if (poly.IsValid) return poly;
            return GeometryFixer.Fix(poly) as Polygon ?? (Polygon?)(poly.Buffer(0) as Polygon);
        }
        catch { return null; }
    }

    // ── geometry transforms (port of geometry.reflect_point_2d / rotate_point_2d) ───
    private static (double x, double z) ApplyTransform(double x, double z, string transform, double cx, double cz)
    {
        if (ReflectionNormals.TryGetValue(transform, out var n)) return Reflect(x, z, n.nx, n.nz, cx, cz);
        if (RotationDeg.TryGetValue(transform, out var deg)) return Rotate(x, z, deg, cx, cz);
        return (x, z);
    }

    private static (double, double) Reflect(double px, double pz, double nx, double nz, double ox, double oz)
    {
        var n2 = nx * nx + nz * nz;
        if (n2 == 0) return (px, pz);
        var d = 2.0 * ((px - ox) * nx + (pz - oz) * nz) / n2;
        return (px - nx * d, pz - nz * d);
    }

    private static (double, double) Rotate(double px, double pz, int degrees, double ox, double oz)
    {
        double dx = px - ox, dz = pz - oz;
        var norm = (((degrees % 360) + 360) % 360);
        (double rx, double rz) = norm switch
        {
            90 => (-dz, dx),
            180 => (-dx, -dz),
            270 => (dz, -dx),
            _ => (dx, dz),
        };
        return (ox + rx, oz + rz);
    }
}
