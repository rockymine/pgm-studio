using PgmStudio.Geom;

namespace PgmStudio.Domain;

/// <summary>
/// The marked square of floor something enters the match on: a player, a wool, a spawner's stack. A pad is
/// laid <b>into</b> the floor rather than standing on it, and its centre is the point the exported element
/// names (<c>WX5</c>) — so the pad is the ground truth and the marker only asks for one.
///
/// <para><b>The size is the marker's parity</b> (<c>WX3</c>). A marker on a block grid line straddles it and
/// takes <see cref="Straddling"/>; one at a block centre sits on it and takes <see cref="Centred"/>, narrowing
/// to <see cref="Narrow"/> where the ground it is fitted into cannot hold the larger square. The pad is always
/// square, which is why a marker whose two axes disagree has no pad at all — <see cref="MixedParity"/>.</para>
///
/// <para><b>It belongs to no structure.</b> Ground that constrains a pad — a room's interior, inset by the
/// clearance it keeps to its walls — is passed to <see cref="Fit"/>; open ground passes none, and the pad then
/// lands exactly where its marker asked.</para>
/// </summary>
/// <param name="MinX">The low block coordinate of the square, inclusive.</param>
/// <param name="MinZ">The low block coordinate on the other axis, inclusive.</param>
/// <param name="Size">Its side, in blocks.</param>
/// <param name="Shifted">Whether the fit moved it off its marker to stay inside the ground it was given
/// (<c>WX4</c>). The exported point follows the pad, so a shift is author-visible.</param>
public readonly record struct SpawnPad(int MinX, int MinZ, int Size, bool Shifted)
{
    /// <summary>The side of a pad whose marker stands on a grid line, which it straddles.</summary>
    public const int Straddling = 2;

    /// <summary>The side of a pad whose marker stands at a block centre, which it is centred on.</summary>
    public const int Centred = 3;

    /// <summary>What a <see cref="Centred"/> pad narrows to where the ground cannot hold it: the marker's own
    /// block and nothing around it.</summary>
    public const int Narrow = 1;

    /// <summary>The pad's centre — the point the export emits. A whole block coordinate for a
    /// <see cref="Straddling"/> pad, a block centre (.5) for the two odd ones.</summary>
    public double CenterX => MinX + Size / 2.0;

    /// <inheritdoc cref="CenterX"/>
    public double CenterZ => MinZ + Size / 2.0;

    /// <summary>Whether <paramref name="coordinate"/> sits on a block grid line (integer) as opposed to a
    /// block centre (.5) — the parity that picks the size.</summary>
    public static bool IsGridLine(double coordinate) => coordinate == Math.Floor(coordinate);

    /// <summary>Whether a marker's block-lattice parity differs between axes (<c>WX3</c>). A pad is square,
    /// so a grid-line x with a block-centre z has no pad and refuses at validation.</summary>
    public static bool MixedParity(double markerX, double markerZ) =>
        IsGridLine(markerX) != IsGridLine(markerZ);

    /// <summary>The nearest offset whose two axes share a parity (<c>WX3</c>). A marker centred in ground that
    /// is odd across one axis only lands mixed, so the block-centre axis moves half a block onto the grid line
    /// below it. Offsets are piece-relative and never negative, which is what the floor holds.</summary>
    public static (double X, double Z) SameParity(double markerX, double markerZ)
    {
        if (!MixedParity(markerX, markerZ)) return (markerX, markerZ);
        return IsGridLine(markerX)
            ? (markerX, Math.Max(0, markerZ - 0.5))
            : (Math.Max(0, markerX - 0.5), markerZ);
    }

    /// <summary>
    /// The pad a marker asks for, inside the ground it is allowed (<c>WX3</c>/<c>WX4</c>): the parity picks
    /// the size, then the square is clamped to keep both ends inside <paramref name="allowed"/> and flags
    /// itself where the clamp moved it.
    ///
    /// <para><paramref name="allowed"/> is the ground a pad must stay within — a room's interior already
    /// inset by the clearance it keeps to its walls. <b>Null is open ground</b>: nothing to clamp to, nothing
    /// to narrow against, so the pad lands on its marker at the size the parity asks for.</para>
    ///
    /// <para>Null where the ground is too small for any pad, which is the caller's to report.</para>
    /// </summary>
    public static SpawnPad? Fit(double markerX, double markerZ, BlockRect? allowed)
    {
        // Open ground holds any square, so it never narrows; ground that holds the centred one on only a
        // single axis narrows both, because the pad is square.
        var holdsCentred = allowed is not { } ground
            || (Centred <= ground.MaxX - ground.MinX && Centred <= ground.MaxZ - ground.MinZ);
        var size = IsGridLine(markerX) ? Straddling
            : holdsCentred ? Centred
            : Narrow;

        int IdealMin(double marker) => IsGridLine(marker)
            ? (int)marker - size / 2
            : (int)Math.Floor(marker) - (size - 1) / 2;
        var idealX = IdealMin(markerX);
        var idealZ = IdealMin(markerZ);
        if (allowed is not { } within) return new SpawnPad(idealX, idealZ, size, Shifted: false);

        if (size > within.MaxX - within.MinX || size > within.MaxZ - within.MinZ) return null;
        var placedX = Math.Min(Math.Max(idealX, within.MinX), within.MaxX - size);
        var placedZ = Math.Min(Math.Max(idealZ, within.MinZ), within.MaxZ - size);
        return new SpawnPad(placedX, placedZ, size, placedX != idealX || placedZ != idealZ);
    }
}
