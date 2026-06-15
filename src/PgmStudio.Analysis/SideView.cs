namespace PgmStudio.Analysis;

/// <summary>
/// Side-view depth projection (B5, port of <c>_build_depth_map</c> in routes/build_regions.py).
/// Projects vertical solid segments onto a 2D (primary × y) grid; each cell holds the nearest
/// depth index normalised to 0–255 (0 = nearest), or -1 for empty. Feeds the side-view canvases (C7).
/// <para>The region can be inspected from any of four directions — the camera on the −/+ side of each
/// axis: <c>nz</c>/<c>pz</c> look along Z (primary = x), <c>nx</c>/<c>px</c> look along X (primary = z).
/// The negative-side cameras (<c>nz</c>/<c>nx</c>) take the smallest coord as the near face; the
/// positive-side cameras (<c>pz</c>/<c>px</c>) take the largest and mirror the primary axis so left/right
/// stays geometrically correct. Legacy <c>z</c>/<c>x</c> are aliases for <c>nz</c>/<c>nx</c>.</para>
/// </summary>
public static class SideView
{
    public sealed record DepthMap(string Axis, int PrimaryMin, int PrimaryCount, int YMin, int YCount, short[] Depth);

    public static readonly string[] Directions = ["nz", "pz", "nx", "px"];

    /// <summary>Build the depth map for a viewing direction, or null when there are no segments.</summary>
    public static DepthMap? Build(IEnumerable<(int x, int z, int ys, int ye)> segments, string dir)
    {
        dir = dir switch { "z" => "nz", "x" => "nx", _ => dir };   // legacy aliases
        bool zLook = dir is "nz" or "pz";    // look along Z → primary = x
        bool nearMin = dir is "nz" or "nx";  // camera on the negative side → nearest = smallest coord
        bool mirror = dir is "pz" or "px";   // viewing from the positive side flips left/right

        var segs = segments as IList<(int x, int z, int ys, int ye)> ?? segments.ToList();
        if (segs.Count == 0) return null;

        int Primary((int x, int z, int ys, int ye) s) => zLook ? s.x : s.z;
        int Depth((int x, int z, int ys, int ye) s) => zLook ? s.z : s.x;

        int pMin = int.MaxValue, pMax = int.MinValue, dMin = int.MaxValue, dMax = int.MinValue,
            yMin = int.MaxValue, yMax = int.MinValue;
        foreach (var s in segs)
        {
            var p = Primary(s); var d = Depth(s);
            if (p < pMin) pMin = p; if (p > pMax) pMax = p;
            if (d < dMin) dMin = d; if (d > dMax) dMax = d;
            if (s.ys < yMin) yMin = s.ys; if (s.ye > yMax) yMax = s.ye;
        }

        int P = pMax - pMin + 1, H = yMax - yMin + 1, D = dMax - dMin + 1;

        // front[p, y] = nearest depth index; D = empty sentinel.
        var front = new int[P * H];
        Array.Fill(front, D);
        foreach (var s in segs)
        {
            var prim = Primary(s);
            var pi = mirror ? pMax - prim : prim - pMin;   // mirror primary for positive-side cameras
            var depthV = Depth(s);
            var di = nearMin ? depthV - dMin : dMax - depthV;   // nearest measured from the camera's side
            var sY = s.ys - yMin;
            var eY = s.ye - yMin + 1;          // exclusive, matches numpy s:e slice
            var rowBase = pi * H;
            for (var y = sY; y < eY; y++)
                if (di < front[rowBase + y]) front[rowBase + y] = di;
        }

        // Normalise to 0–255 (float32 path, matching numpy), empty → -1.
        var depth = new short[P * H];
        for (var i = 0; i < depth.Length; i++)
        {
            if (front[i] >= D) { depth[i] = -1; continue; }
            depth[i] = D > 1 ? (short)((float)front[i] / (D - 1) * 255f + 0.5f) : (short)0;
        }

        return new DepthMap(dir, pMin, P, yMin, H, depth);
    }
}
