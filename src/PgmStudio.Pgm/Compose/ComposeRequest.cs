namespace PgmStudio.Pgm.Compose;

/// <summary>
/// The composer's input: how many players, how many teams, which symmetry, and the seed driving every random
/// choice downstream. Validated at construction — bad combinations (an unsupported team count, a symmetry
/// mode that team count can't fan) throw immediately rather than surfacing as a confusing failure deep inside
/// generation.
/// </summary>
public sealed class ComposeRequest
{
    public int PlayersPerTeam { get; }
    public int Teams { get; }
    public string Symmetry { get; }
    public ulong Seed { get; }
    public int Cell { get; }

    /// <param name="playersPerTeam">Clamped to 5..32 (the envelope's calibrated range).</param>
    /// <param name="teams">2 or 4.</param>
    /// <param name="symmetry">Null selects the default for <paramref name="teams"/>: <c>rot_180</c> for 2,
    /// <c>rot_90</c> for 4. <c>mirror_x</c>/<c>mirror_z</c> are legal only for 2 teams.</param>
    /// <param name="seed">Drives every random draw the composer makes; the same seed reproduces the same plan.</param>
    /// <param name="cell">Blocks per proxy cell (the plan grid scale).</param>
    public ComposeRequest(int playersPerTeam, int teams = 2, string? symmetry = null, ulong seed = 0, int cell = 5)
    {
        PlayersPerTeam = Math.Clamp(playersPerTeam, 5, 32);

        if (teams != 2 && teams != 4)
            throw new ArgumentException($"teams must be 2 or 4 (got {teams})", nameof(teams));
        Teams = teams;

        Symmetry = symmetry ?? (teams == 4 ? "rot_90" : "rot_180");
        if (teams == 4 && Symmetry != "rot_90")
            throw new ArgumentException($"4-team boards require rot_90 symmetry (got '{Symmetry}')", nameof(symmetry));
        if (teams == 2 && Symmetry is not ("rot_180" or "mirror_x" or "mirror_z"))
            throw new ArgumentException(
                $"2-team symmetry must be rot_180, mirror_x, or mirror_z (got '{Symmetry}')", nameof(symmetry));

        Seed = seed;

        if (cell <= 0) throw new ArgumentException("cell must be positive", nameof(cell));
        Cell = cell;
    }
}
