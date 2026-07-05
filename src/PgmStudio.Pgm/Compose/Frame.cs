namespace PgmStudio.Pgm.Compose;

/// <summary>
/// The generalized (axis-normal, cross) coordinate frame a symmetry mode grows its authored unit in. <c>u</c>
/// is distance from the symmetry axis, increasing away from it; <c>v</c> is the cross-axis coordinate,
/// centred near 0. <see cref="PrimaryAxis"/> names the real axis <c>u</c> rides on (<c>z</c> for
/// <c>rot_180</c>/<c>mirror_z</c>/<c>rot_90</c>, whose unit occupies the +z half/wedge; <c>x</c> for
/// <c>mirror_x</c>, whose unit occupies the −x half); <see cref="Sign"/> is the real-axis direction of
/// increasing <c>u</c> (+1 or −1). Growing every role (hub, lanes, frontline) in (u,v) instead of raw (x,z)
/// lets the same grower code serve every symmetry mode without duplicated per-mode branches.
/// </summary>
internal readonly record struct Frame(char PrimaryAxis, int Sign)
{
    public static Frame For(string symmetry) => symmetry == "mirror_x" ? new Frame('x', -1) : new Frame('z', +1);

    /// <summary>The absolute board direction (<c>front</c>/<c>back</c>/<c>left</c>/<c>right</c> — facing is
    /// always an absolute board direction, fanned per orbit image) a marker on this unit faces to look toward
    /// the symmetry axis, i.e. toward the enemy (SP3's default).</summary>
    public string TowardAxis => PrimaryAxis == 'z'
        ? (Sign > 0 ? "front" : "back")
        : (Sign > 0 ? "left" : "right");

    /// <summary>Map a generalized point to a real (x,z) point (both axes may be fractional — used for marker
    /// offsets on the half-cell lattice).</summary>
    public (double X, double Z) ToPoint(double u, double v) =>
        PrimaryAxis == 'z' ? (v, Sign * u) : (Sign * u, v);

    /// <summary>Map a generalized rect (<c>uMin..uMin+uSpan</c>, <c>vMin..vMin+vSpan</c>) to a real cell rect
    /// <c>[x, z, w, h]</c> (<see cref="PlanPiece.Rect"/> convention).</summary>
    public int[] ToRect(int uMin, int uSpan, int vMin, int vSpan)
    {
        var (x1, z1) = ToPoint(uMin, vMin);
        var (x2, z2) = ToPoint(uMin + uSpan, vMin + vSpan);
        int minX = (int)Math.Round(Math.Min(x1, x2)), maxX = (int)Math.Round(Math.Max(x1, x2));
        int minZ = (int)Math.Round(Math.Min(z1, z2)), maxZ = (int)Math.Round(Math.Max(z1, z2));
        return [minX, minZ, maxX - minX, maxZ - minZ];
    }

    /// <summary>The piece-relative offset (a <see cref="SpawnPlacement.At"/>/<see cref="WoolPlacement.At"/>
    /// value) of the generalized point (u,v) within the piece occupying (uMin,uSpan,vMin,vSpan).</summary>
    public double[] LocalAt(int uMin, int uSpan, int vMin, int vSpan, double u, double v)
    {
        var rect = ToRect(uMin, uSpan, vMin, vSpan);
        var (x, z) = ToPoint(u, v);
        return [x - rect[0], z - rect[1]];
    }

    /// <summary>The inverse of <see cref="ToRect"/>: recover a cell rect's generalized
    /// (uMin, uSpan, vMin, vSpan) interval pair.</summary>
    public (int UMin, int USpan, int VMin, int VSpan) FromRect(int[] rect) => PrimaryAxis == 'z'
        ? (Sign > 0 ? rect[1] : -(rect[1] + rect[3]), rect[3], rect[0], rect[2])
        : (Sign > 0 ? rect[0] : -(rect[0] + rect[2]), rect[2], rect[1], rect[3]);
}
