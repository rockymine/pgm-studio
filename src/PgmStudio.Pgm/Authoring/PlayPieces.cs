using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Authoring;

/// <summary>One play piece where the sketch draws it: its id, its kind, the colour it is told apart by, the
/// rectangle it stands on, and whether it moves only with something else.</summary>
/// <param name="Id">The piece's id, which the piece route moves it by.</param>
/// <param name="Kind">What it is, a <see cref="PlayPieceKinds"/> word.</param>
/// <param name="Color">The team or dye colour it is drawn in, or empty.</param>
/// <param name="Rect">Where it stands, in blocks.</param>
/// <param name="Carried">True where the piece does not move on its own: an image the symmetry draws and the
/// intent does not store, or an iron cube seated in a room.</param>
/// <param name="Line">A wall's two ends, <c>[x1, z1, x2, z2]</c> on block corners; null for every other piece.</param>
public readonly record struct PlayPiece(string Id, string Kind, string Color, Rect Rect, bool Carried, int[]? Line = null);

/// <summary>
/// Every play piece of an intent as the sketch draws it, each at the rectangle the piece route reads it as
/// standing on, so a drag of the drawn rectangle is a move the route accepts.
/// </summary>
public static class PlayPieces
{
    /// <summary>The pieces of <paramref name="intent"/>, its symmetry's images included.</summary>
    public static IEnumerable<PlayPiece> Of(MapIntent intent)
    {
        var stored = new HashSet<string>(PlayIds.Stated(PlayIds.Assign(intent)), StringComparer.Ordinal);
        var expanded = PlayIds.Assign(SymmetryExpander.Expand(intent));
        string TeamColor(string team) => expanded.Teams?.FirstOrDefault(t => t.Id == team)?.Color ?? team;
        PlayPiece Piece(string id, string kind, string color, Rect rect) => new(id, kind, color, rect, !stored.Contains(id));

        foreach (var spawn in expanded.Spawns)
        {
            if (spawn.Protection.Count > 0)
            {
                yield return Piece(spawn.Id, PlayPieceKinds.Spawn, TeamColor(spawn.Team), spawn.Protection[0]);
                yield return new PlayPiece(PlayIds.Marker(spawn.Id), PlayPieceKinds.SpawnPoint, TeamColor(spawn.Team),
                                           Marker(spawn.Point), !stored.Contains(spawn.Id));
            }
            for (var at = 0; at < spawn.Iron.Count; at++)
                yield return new PlayPiece($"{spawn.Id}-iron-{at + 1}", PlayPieceKinds.IronCube, TeamColor(spawn.Team),
                                           Iron(RoomFrames.IronCorner(spawn.Iron[at].X), RoomFrames.IronCorner(spawn.Iron[at].Z)),
                                           true);
        }
        foreach (var wool in expanded.Wools ?? [])
            if (wool.Protection.Count > 0)
            {
                var color = wool.Color.Length > 0 ? wool.Color : TeamColor(wool.Owner);
                yield return Piece(wool.Id, PlayPieceKinds.WoolRoom, color, wool.Protection[0]);
                yield return new PlayPiece(PlayIds.Marker(wool.Id), PlayPieceKinds.WoolSpawn, color, Marker(wool.Spawn),
                                           !stored.Contains(wool.Id));
            }
        foreach (var goal in expanded.Destroyables ?? [])
            yield return Piece(goal.Id, PlayPieceKinds.Destroyable, TeamColor(goal.Owner), Destroyable(goal));
        foreach (var core in expanded.Cores ?? [])
            yield return Piece(core.Id, PlayPieceKinds.Core, TeamColor(core.Owner), Core(core));
        foreach (var area in expanded.Build?.Areas ?? [])
            yield return Piece(area.Id, PlayPieceKinds.BuildArea, "", area.Rect);
        foreach (var lane in expanded.WaterLanes?.Rects ?? [])
            yield return Piece(lane.Id, PlayPieceKinds.WaterLane, "", lane.Rect);
        foreach (var cube in expanded.Structures?.IronCubes ?? [])
            yield return Piece(cube.Id, PlayPieceKinds.IronCube, "", Iron(cube.MinX, cube.MinZ));
        foreach (var wall in expanded.Structures?.Walls ?? [])
            yield return Piece(wall.Id, PlayPieceKinds.Wall, "", Wall(wall)) with { Line = [wall.X1, wall.Z1, wall.X2, wall.Z2] };
        foreach (var chest in expanded.Structures?.Chests ?? [])
            yield return Piece(chest.Id, PlayPieceKinds.Chest, "", Chest(chest));
        if (expanded.Observer is { } observer)
            yield return Piece(PlayIds.Observer, PlayPieceKinds.Observer, "", Observer(observer));
    }

    /// <summary>A destroyable's footprint: its structure centred on the anchor, in whole blocks.</summary>
    public static Rect Destroyable(DestroyableIntent goal)
    {
        var (width, _, depth) = ObjectiveFootprint.Destroyable(
            DestroyableStyles.TryParse(goal.Style, out var style) ? style : ObjectiveDefaults.Style);
        return Centred(goal.Anchor, width, depth);
    }

    /// <summary>A core's footprint, the same way.</summary>
    public static Rect Core(CoreIntent core)
    {
        var (width, depth) = ObjectiveFootprint.Core(core.Size);
        return Centred(core.Anchor, width, depth);
    }

    /// <summary>An iron cube's footprint from its min corner.</summary>
    public static Rect Iron(int minX, int minZ) => new(minX, minZ, minX + RoomFrames.IronSpan, minZ + RoomFrames.IronSpan);

    /// <summary>The rectangle a wall's columns span.</summary>
    public static Rect Wall(WallStructure wall)
    {
        var bounds = wall.Bounds();
        return new Rect(bounds.MinX, bounds.MinZ, bounds.MaxX, bounds.MaxZ);
    }

    /// <summary>The block a defence chest stands in.</summary>
    public static Rect Chest(ChestMarker chest) => new(chest.X, chest.Z, chest.X + 1, chest.Z + 1);

    /// <summary>A room's marker: the block-wide square centred on its point.</summary>
    public static Rect Marker(Pt point) => new(point.X - 0.5, point.Z - 0.5, point.X + 0.5, point.Z + 0.5);

    /// <summary>The block the observer stands in.</summary>
    public static Rect Observer(ObserverIntent observer) =>
        new(Math.Floor(observer.Point.X), Math.Floor(observer.Point.Z),
            Math.Floor(observer.Point.X) + 1, Math.Floor(observer.Point.Z) + 1);

    private static Rect Centred(Pt anchor, int width, int depth)
    {
        var (minX, minZ, maxX, maxZ) = ObjectiveFootprint.Centred(anchor.X, anchor.Z, width, depth);
        return new Rect(minX, minZ, maxX + 1, maxZ + 1);
    }
}
