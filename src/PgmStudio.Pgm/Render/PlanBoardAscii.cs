using System.Text;
using PgmStudio.Geom;
using PgmStudio.Geom.Render;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Render;

/// <summary>
/// A plan drawn as a grid of characters — the same fanned board <see cref="PlanBoardPng"/> and
/// <see cref="PlanBoardSvg"/> draw, off the same <see cref="PlanBoardScene"/>, in the one form a reader with
/// no eye on a picture can act on.
///
/// <para>A plan is not a picture: it is a list of rectangles measured in cells, and most of what goes wrong
/// with one is a <b>relation between two of them</b> — a landform wider than the band that reaches it, a wall
/// on the only throat, a room whose door opens onto its own apron. A render of the built world cannot show a
/// relation between two rectangles, because by then they are terrain. A grid puts them on the same rows, and
/// the relation is one glance.</para>
///
/// <para>Each cell is one character. Terrain takes a letter assigned per piece, so the same piece reads the
/// same everywhere and its orbit images read alike; a build zone is <c>+</c>, a water lane <c>~</c>, an
/// enclosed void <c>o</c>, open void a space. Markers overprint their terrain, and an
/// <see cref="Overlay"/> — a route, a corridor, a cut — overprints everything, because the reason to draw one
/// is to see where it lies against the ground.</para>
/// </summary>
public static class PlanBoardAscii
{
    /// <summary>Cells drawn over the board, painted in the order given: later layers win. The
    /// <see cref="Glyph"/> is one character and <see cref="Name"/> is what the key calls it.</summary>
    public sealed record Overlay(string Name, char Glyph, IReadOnlyCollection<(int X, int Z)> Cells);

    public const char BuildZone = '+';
    public const char WaterLane = '~';
    public const char EnclosedVoid = 'o';
    public const char OpenVoid = ' ';

    private static readonly Dictionary<string, char> MarkerGlyphs = new()
    {
        ["spawn"] = '@', ["wool"] = '\u00a4', ["iron"] = 'i', ["destroyable"] = '!', ["core"] = 'O',
    };

    /// <summary><b>every</b> is draw one character per <paramref name="every"/> cells, sampling the top-left of
    /// each block. A board wider than a terminal is unreadable at 1:1, and a plan's faults survive a halving —
    /// but a route one cell wide can be sampled away, so the default is the honest 1:1 and the caller decides.
    /// <para><b>width</b> — Wrap the key at this column.</para></summary>
    public static string Render(PlanModel plan, IReadOnlyList<Overlay>? overlays = null, int every = 1, int width = 100)
    {
        var scene = PlanBoardScene.Build(plan);
        if (scene is null) return "(the plan draws nothing: no piece and no zone)\n";
        if (every < 1) every = 1;

        int minX = scene.MinX, minZ = scene.MinZ, nx = scene.Width, nz = scene.Height;
        var glyph = new char[nx, nz];
        for (var x = 0; x < nx; x++) for (var z = 0; z < nz; z++) glyph[x, z] = OpenVoid;

        // Zones first, terrain over them: a piece standing in a build zone is ground, not a crossing.
        var lettering = new Lettering();
        var key = new SortedDictionary<char, string>();
        foreach (var zone in scene.Zones)
            Fill(glyph, nx, nz, minX, minZ, zone.Rect, zone.Lane ? WaterLane : BuildZone);
        foreach (var cell in scene.ZoneHoles) Set(glyph, nx, nz, minX, minZ, cell, OpenVoid);
        foreach (var piece in scene.Pieces)
        {
            var letter = lettering.For(piece.Id, piece.Role);
            key[letter] = $"{piece.Id} ({piece.Role})";
            Fill(glyph, nx, nz, minX, minZ, piece.Rect, letter);
        }

        // Enclosed void: a pocket the outside cannot reach is a hole, and a hole is the feature a route may
        // have a way round — so it is named rather than left as blank space.
        var solid = new HashSet<(int X, int Z)>();
        for (var x = 0; x < nx; x++)
            for (var z = 0; z < nz; z++)
                if (glyph[x, z] != OpenVoid) solid.Add((minX + x, minZ + z));
        foreach (var cell in Enclosed(solid, nx, nz, minX, minZ)) Set(glyph, nx, nz, minX, minZ, cell, EnclosedVoid);

        foreach (var marker in scene.Markers)
        {
            if (!MarkerGlyphs.TryGetValue(marker.Kind, out var mark)) continue;
            key[mark] = marker.Kind;
            Set(glyph, nx, nz, minX, minZ, ((int)Math.Floor(marker.X), (int)Math.Floor(marker.Z)), mark);
        }

        foreach (var overlay in overlays ?? [])
        {
            key[overlay.Glyph] = overlay.Name;
            foreach (var cell in overlay.Cells) Set(glyph, nx, nz, minX, minZ, cell, overlay.Glyph);
        }

        return Compose(glyph, nx, nz, minX, minZ, every, key, plan.Globals.Cell, width);
    }

    /// <summary>The one-line answer a relation wants: two cell sets laid on the same rows, so a reader can
    /// count one against the other. <c>Rows</c> is the z range drawn.</summary>
    public static string Compare(string title, params (string Name, char Glyph, IReadOnlyCollection<(int X, int Z)> Cells)[] layers)
    {
        var all = layers.SelectMany(l => l.Cells).ToList();
        if (all.Count == 0) return $"{title}: nothing to draw\n";
        var bounds = Cells.BoundingBox(all);
        var text = new StringBuilder();
        text.Append(title).Append('\n');
        for (var z = bounds.Z; z < bounds.MaxZ; z++)
        {
            text.Append($"{z,5} | ");
            for (var x = bounds.X; x < bounds.MaxX; x++)
            {
                var mark = OpenVoid;
                foreach (var layer in layers) if (layer.Cells.Contains((x, z))) mark = layer.Glyph;
                text.Append(mark);
            }
            text.Append(" |\n");
        }
        foreach (var layer in layers)
            text.Append($"      {layer.Glyph} {layer.Name} — {layer.Cells.Count} cells\n");
        return text.ToString();
    }

    private static string Compose(char[,] glyph, int nx, int nz, int minX, int minZ, int every,
        SortedDictionary<char, string> key, int cell, int width)
    {
        var text = new StringBuilder();
        var columns = (nx + every - 1) / every;

        TextGrid.Ruler(text, "      | ", minX, columns, every, " |");

        for (var i = 0; i < (nz + every - 1) / every; i++)
        {
            var z = minZ + i * every;
            text.Append($"{z,5} | ");
            for (var j = 0; j < columns; j++) text.Append(glyph[j * every, i * every]);
            text.Append(" |\n");
        }

        text.Append($"      {columns} × {(nz + every - 1) / every} cells drawn");
        if (every > 1) text.Append($" at 1:{every}");
        text.Append($"  ·  {nx} × {nz} cells = {nx * cell} × {nz * cell} blocks  ·  cell {cell}\n");

        var line = new StringBuilder("      ");
        foreach (var (mark, name) in key)
        {
            var entry = $"{mark} {name}   ";
            if (line.Length + entry.Length > width) { text.Append(line).Append('\n'); line = new StringBuilder("      "); }
            line.Append(entry);
        }
        if (line.ToString().Trim().Length > 0) text.Append(line).Append('\n');
        return text.ToString();
    }

    private static void Fill(char[,] glyph, int nx, int nz, int minX, int minZ, CellRect rect, char mark)
    {
        for (var x = rect.X; x < rect.X + rect.Width; x++)
            for (var z = rect.Z; z < rect.Z + rect.Height; z++)
                Set(glyph, nx, nz, minX, minZ, (x, z), mark);
    }

    private static void Set(char[,] glyph, int nx, int nz, int minX, int minZ, (int X, int Z) cell, char mark)
    {
        int x = cell.X - minX, z = cell.Z - minZ;
        if (x >= 0 && x < nx && z >= 0 && z < nz) glyph[x, z] = mark;
    }

    private static IEnumerable<(int X, int Z)> Enclosed(IReadOnlySet<(int X, int Z)> solid, int nx, int nz, int minX, int minZ)
    {
        var outside = new HashSet<(int X, int Z)>();
        var queue = new Queue<(int X, int Z)>();
        void Seed(int x, int z)
        {
            var cell = (minX + x, minZ + z);
            if (!solid.Contains(cell) && outside.Add(cell)) queue.Enqueue(cell);
        }
        for (var x = 0; x < nx; x++) { Seed(x, 0); Seed(x, nz - 1); }
        for (var z = 0; z < nz; z++) { Seed(0, z); Seed(nx - 1, z); }
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var neighbour in Cells.N4(current))
            {
                int x = neighbour.Item1 - minX, z = neighbour.Item2 - minZ;
                if (x < 0 || x >= nx || z < 0 || z >= nz) continue;
                if (!solid.Contains(neighbour) && outside.Add(neighbour)) queue.Enqueue(neighbour);
            }
        }
        for (var x = 0; x < nx; x++)
            for (var z = 0; z < nz; z++)
            {
                var cell = (minX + x, minZ + z);
                if (!solid.Contains(cell) && !outside.Contains(cell)) yield return cell;
            }
    }

    /// <summary>Assigns a stable letter per piece id. Role decides the case and the run a piece draws from, so
    /// a spawn reads as a spawn at a glance and two pieces of one box read alike — a board is scanned for what
    /// a thing <em>is</em> before it is read for which one it is.</summary>
    private sealed class Lettering
    {
        private static readonly (string Role, string Run)[] Runs =
        [
            (PlanRoles.Spawn, "SXYZ"),
            (PlanRoles.WoolRoom, "WVU"),
        ];
        private const string General = "ABCDEFGHJKLMNPQRT";

        private readonly Dictionary<string, char> _assigned = [];
        private readonly Dictionary<string, int> _next = [];

        public char For(string id, string role)
        {
            if (_assigned.TryGetValue(id, out var letter)) return letter;
            var run = Runs.FirstOrDefault(r => r.Role == role).Run ?? General;
            var taken = _next.GetValueOrDefault(run);
            _next[run] = taken + 1;
            letter = taken < run.Length ? run[taken] : char.ToLowerInvariant(General[taken % General.Length]);
            _assigned[id] = letter;
            return letter;
        }
    }
}
