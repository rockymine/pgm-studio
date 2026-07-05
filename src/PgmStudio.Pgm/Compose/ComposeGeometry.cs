using PgmStudio.Geom;

namespace PgmStudio.Pgm.Compose;

/// <summary>
/// The shared fanned-board separation rule every composition step validates against. Two piece classes:
/// <b>land</b> pieces (the connected team unit — they may touch each other within one orbit image) and
/// <b>isolated</b> pieces (mid stones, severed plateaus — reachable only by building, never by walking).
/// Pieces of different orbit images, and any pair involving an isolated piece, must keep at least
/// <see cref="TeamUnitGrower.ImageClearanceBlocks"/> of clearance on some axis: a shared border, however
/// narrow, would weld what must stay separate islands (CT1), and anything under G5's minimum hop leaves no
/// room to bridge.
/// </summary>
internal static class ComposeGeometry
{
    /// <summary>True when every fanned pair satisfies the separation rule: same-image land↔land pairs may
    /// touch but never overlap; every other pair (different images, or involving an isolated piece) keeps
    /// the 10-block clearance on at least one axis.</summary>
    internal static bool SeparationOk(
        ComposeEnvelope env, IReadOnlyList<int[]> landRects, IReadOnlyList<int[]> isolatedRects,
        IReadOnlyList<int[]>? centerRects = null)
    {
        var order = Symmetry.Order(env.Symmetry);
        var axes = Symmetry.OrbitAxes(env.Symmetry);
        var images = new List<(int K, bool Isolated, bool Center, double X1, double Z1, double X2, double Z2)>();

        void Add(int[] rect, bool isolated, bool center)
        {
            double x1 = rect[0] * env.Cell, z1 = rect[1] * env.Cell;
            double x2 = (rect[0] + rect[2]) * env.Cell, z2 = (rect[1] + rect[3]) * env.Cell;
            for (var k = 0; k < order; k++)
            {
                var (ix1, iz1, ix2, iz2) = FanImage(x1, z1, x2, z2, axes, k);
                images.Add((k, isolated, center, ix1, iz1, ix2, iz2));
            }
        }
        foreach (var r in landRects) Add(r, false, false);
        foreach (var r in isolatedRects) Add(r, true, false);
        if (centerRects is not null) foreach (var r in centerRects) Add(r, true, true);

        const double clear = TeamUnitGrower.ImageClearanceBlocks;
        const double eps = 1e-6;
        for (var i = 0; i < images.Count; i++)
            for (var j = i + 1; j < images.Count; j++)
            {
                var (a, b) = (images[i], images[j]);
                var ix = Math.Min(a.X2, b.X2) - Math.Max(a.X1, b.X1);
                var iz = Math.Min(a.Z2, b.Z2) - Math.Max(a.Z1, b.Z1);

                // A centre stone straddles the axis: its fanned images abut there (share a border) or coincide
                // (identical rect) — one physical island, not a collision (CT11). Interior overlap still welds.
                if (a.Center && b.Center)
                {
                    var identical = Math.Abs(a.X1 - b.X1) < eps && Math.Abs(a.Z1 - b.Z1) < eps
                                    && Math.Abs(a.X2 - b.X2) < eps && Math.Abs(a.Z2 - b.Z2) < eps;
                    var interiorOverlap = ix > eps && iz > eps;
                    if (identical) continue;                             // same physical centre stone
                    if (!interiorOverlap && ix > -eps && iz > -eps) continue;   // abut at the axis → connected
                    // otherwise (interior overlap, or disjoint) falls through to the clearance rule below
                }

                if (a.K == b.K && !a.Isolated && !b.Isolated)
                {
                    if (ix > eps && iz > eps) return false;                    // overlap within one image
                }
                else if (ix > -clear + eps && iz > -clear + eps)
                    return false;                                             // welded or unbridgeably close
            }
        return true;
    }

    /// <summary>The k-th orbit image of a rect: identity at k=0, the mode's concrete orbit axes for
    /// k=1..order-1 — matching <see cref="Plan.PlanDerived.FanRect"/> (unlike the k-agnostic
    /// <see cref="Symmetry.Rect"/>).</summary>
    internal static (double X1, double Z1, double X2, double Z2) FanImage(
        double x1, double z1, double x2, double z2, string[] axes, int k)
    {
        if (k == 0) return (x1, z1, x2, z2);
        (double x, double z)[] corners = [(x1, z1), (x1, z2), (x2, z1), (x2, z2)];
        var axis = axes[k - 1];
        var pts = corners.Select(c => Symmetry.Apply(c.x, c.z, axis, 0, 0)).ToList();
        return (pts.Min(p => p.X), pts.Min(p => p.Z), pts.Max(p => p.X), pts.Max(p => p.Z));
    }
}
