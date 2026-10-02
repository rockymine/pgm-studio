using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Geom.Render;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Render;

/// <summary>Where an eye stands and where it looks. <paramref name="Yaw"/> is the game's own: 0 looks south
/// (+z), 90 west (−x), 180 north, −90 east, within (−180, 180]. <paramref name="Pitch"/> is degrees below the horizon, negative
/// looking up. <paramref name="Fov"/> is the horizontal field of view in degrees.</summary>
public readonly record struct EyeCamera(double X, double Y, double Z, double Yaw, double Pitch, double Fov = 70);

/// <summary>What one pixel's ray hits: the block, in world coordinates, and the ground under it — the highest
/// ground at or below the block, which for a floating tree's leaves is the grass the tree should stand on.
/// <paramref name="Ground"/> is null where the column under the block holds none.</summary>
public readonly record struct EyeHit((int X, int Y, int Z) Block, (int X, int Y, int Z)? Ground);

/// <summary>The ground a set of pixels' rays hit: every column once, at the highest height a ray hit it, and
/// how many of the pixels hit nothing.</summary>
public sealed record EyeArea(IReadOnlyDictionary<(int X, int Z), int> Columns, int Sky);

/// <summary>One block and the share of the picture's pixels it fills.</summary>
public readonly record struct SeenBlock(int Id, int Data, double Share);

/// <summary>A drawn picture and what is in it: <paramref name="Rgb"/> three bytes a pixel, the blocks hit by
/// share of the frame, the share that is sky, and the share drawn in a palette colour because no sprite
/// names that block.</summary>
public sealed record EyePicture(int Width, int Height, byte[] Rgb, IReadOnlyList<SeenBlock> Seen,
                                double Sky, double Untextured)
{
    public byte[] Png() => PngWriter.Encode(Width, Height, Rgb);
}

/// <summary>
/// A built world seen from a player's eye, drawn with the game's own block sprites.
///
/// <para>Every other read draws a block as one colour, which is the right answer for a plan and the wrong
/// one for a finish: two noisy blocks of one colour are one calm grey in a flat picture and static on the
/// ground, and the sprite is what a player looks at. This draws the sprite, in perspective, at the height a
/// player's eye stands — and <c>flat</c> draws the same frame with every sprite replaced by its own mean, so
/// the difference texture makes is itself a picture.</para>
///
/// <para>A ray is walked cell by cell through a dense copy of the world. A cube shows the sprite of the face
/// the ray entered through, shaded by which way that face points; a plant is two crossed quads; a texel
/// with no alpha lets the ray through, which is what makes leaves and glass read as they do. A slab, a
/// stair, a fence, a pane, a wall or a chest fills only the boxes <see cref="BlockShape"/> names for it, a
/// fence, a pane or a wall reaching out to the neighbours it meets, and a chest wearing its front on the side
/// it looks toward. A torch is crossed quads like a plant; a carpet, a wire, a ladder, a vine, a lily pad, a
/// rail and a pressure plate are sheets, and a door, a trapdoor, a sign, a button, a lever and a flower pot are
/// boards and boxes of their own, all drawn but neither stood on nor in the way of a line of sight. A hopper,
/// a cauldron, an anvil, a bed, a cake, a snow layer and the like are boxes with body to them. Far ground fades into the sky,
/// and a block no sprite is named for is drawn in its palette colour and counted as such.</para>
/// </summary>
public sealed class EyeScene
{
    private const int SkyRgb = 0x9EC0F2;
    /// <summary>How far a ray is followed once it is in the world: sixteen chunks of render distance, with no
    /// fog before it — the game as it is played with fog off. Counted from where the ray enters the world's box,
    /// so an eye raised over the board sees as far into it as one standing on it.</summary>
    private const double FarEnough = 256;
    private const double EyeHeight = 1.62;

    /// <summary>How far over a thing's top a framing eye rises, looking for a place in the air that sees it.</summary>
    private const int HoverHighest = 43;

    private readonly ushort[] _cells;
    private readonly Material[] _materials;
    private readonly int _minX, _minZ, _width, _height, _depth;

    /// <summary>How one block is drawn. <paramref name="Solid"/> is whether it stands in the way — a cube or a
    /// shape with body to it, not a plant or a sheet — and <paramref name="Ground"/> whether it is solid ground
    /// a player stands on rather than a tree's crown.</summary>
    private sealed record Material(int Id, int Data, FaceForm Form, BlockSprite? Top, BlockSprite? Side,
                                   bool Untextured, bool Solid, bool Ground, CellBox[]? Boxes, Grain Grain,
                                   BlockSprite? Front = null, RoomEdge Facing = RoomEdge.NegZ);

    private EyeScene(ushort[] cells, Material[] materials, int minX, int minZ, int width, int height, int depth)
    {
        _cells = cells; _materials = materials;
        _minX = minX; _minZ = minZ; _width = width; _height = height; _depth = depth;
    }

    /// <summary>The world, ready to be looked at. <paramref name="flat"/> swaps every sprite for its own mean
    /// colour and changes nothing else.</summary>
    public static EyeScene Of(VoxelWorld world, BlockTextureSet textures, bool flat = false)
    {
        var chunks = world.Chunks;
        if (chunks.Count == 0) return new EyeScene([], [null!], 0, 0, 0, 0, 0);
        int minCx = chunks.Keys.Min(key => key.Cx), maxCx = chunks.Keys.Max(key => key.Cx);
        int minCz = chunks.Keys.Min(key => key.Cz), maxCz = chunks.Keys.Max(key => key.Cz);
        var topSection = chunks.Values.Max(chunk => Array.FindLastIndex(chunk.Ids, section => section is not null));
        int minX = minCx * 16, minZ = minCz * 16;
        int width = (maxCx - minCx + 1) * 16, depth = (maxCz - minCz + 1) * 16;
        var height = Math.Min(VoxelWorld.MaxHeight, (topSection + 1) * 16 + 16);

        var cells = new ushort[width * height * depth];
        var materials = new List<Material> { null! };
        var index = new Dictionary<(int Id, int Data, uint Tint, Joins Joins), ushort>();
        var sprites = new SpriteCache(textures, flat);
        var joining = new List<(int X, int Y, int Z)>();

        foreach (var ((cx, cz), chunk) in chunks)
            for (var sy = 0; sy < chunk.Ids.Length; sy++)
            {
                if (chunk.Ids[sy] is not { } ids) continue;
                var data = chunk.Data[sy];
                for (var i = 0; i < 4096; i++)
                {
                    int id = ids[i];
                    if (id == 0) continue;
                    int x = cx * 16 + (i & 15), z = cz * 16 + ((i >> 4) & 15), y = sy * 16 + (i >> 8);
                    int nibble = data?[i] ?? 0;
                    if (BlockShape.IsDoor(id))
                    {
                        var (otherId, otherData) = world.GetBlock(x, y + ((nibble & 8) != 0 ? -1 : 1), z);
                        nibble = BlockShape.DoorData(nibble, otherId == id ? otherData : 0);
                    }
                    var faces = id == 175 && (nibble & 8) != 0
                        ? BlockFaces.UpperHalf(world.GetBlock(x, y - 1, z).Data)
                        : BlockFaces.Of(id, nibble);
                    if (faces is { Form: FaceForm.Hidden }) continue;
                    var tint = Tint(faces, id, nibble, world.GetBiome(x, z), x, z);
                    if (!index.TryGetValue((id, nibble, tint, Joins.None), out var slot))
                    {
                        slot = (ushort)materials.Count;
                        index[(id, nibble, tint, Joins.None)] = slot;
                        materials.Add(Describe(id, nibble, faces, tint, sprites));
                    }
                    cells[((x - minX) * height + y) * depth + (z - minZ)] = slot;
                    if (BlockShape.Joining(id) != JoinKind.None) joining.Add((x - minX, y, z - minZ));
                }
            }
        Join(cells, materials, index, joining, width, height, depth);
        return new EyeScene(cells, [.. materials], minX, minZ, width, height, depth);
    }

    /// <summary>Gives every fence, pane and wall the shape of the neighbours it meets, once every cell is
    /// known.</summary>
    private static void Join(ushort[] cells, List<Material> materials,
        Dictionary<(int Id, int Data, uint Tint, Joins Joins), ushort> index, List<(int X, int Y, int Z)> joining,
        int width, int height, int depth)
    {
        Material? Neighbour(int x, int y, int z) =>
            x >= 0 && x < width && z >= 0 && z < depth && cells[(x * height + y) * depth + z] is var slot and > 0
                ? materials[slot] : null;

        var tints = index.ToDictionary(entry => entry.Value, entry => entry.Key.Tint);
        var updates = new List<(int Cell, ushort Slot)>();
        foreach (var (x, y, z) in joining)
        {
            var at = (x * height + y) * depth + z;
            var material = materials[cells[at]];
            var kind = BlockShape.Joining(material.Id);
            var joins = Joins.None;
            foreach (var (side, dx, dz) in (ReadOnlySpan<(Joins, int, int)>)
                     [(Joins.North, 0, -1), (Joins.South, 0, 1), (Joins.West, -1, 0), (Joins.East, 1, 0)])
                if (Neighbour(x + dx, y, z + dz) is { } neighbour
                    && BlockShape.Meets(kind, neighbour.Id, neighbour is { Form: FaceForm.Cube, Boxes: null }))
                    joins |= side;
            if (joins == Joins.None) continue;
            var key = (material.Id, material.Data, tints[cells[at]], joins);
            if (!index.TryGetValue(key, out var slot))
            {
                slot = (ushort)materials.Count;
                index[key] = slot;
                materials.Add(material with { Boxes = BlockShape.Of(material.Id, material.Data, joins) });
            }
            updates.Add((at, slot));
        }
        foreach (var (cell, slot) in updates) cells[cell] = slot;
    }

    private static uint Tint(BlockFaces? faces, int id, int data, byte biome, int x, int z)
    {
        if (faces?.Tint is { } fixedTint) return fixedTint;
        var channel = BlockTints.Of(id, data);
        return channel == TintChannel.None ? 0xFFFFFF : BiomeTint.Of(biome, channel, x, z);
    }

    private static Material Describe(int id, int data, BlockFaces? faces, uint tint, SpriteCache sprites)
    {
        var boxes = BlockShape.Of(id, data);
        var sheet = BlockShape.Sheet(boxes);
        var detail = sheet || BlockShape.Detail(id);
        var ground = faces is not { Form: FaceForm.Cross } && !detail && id is not (Blocks.Leaves or Blocks.Leaves2);
        if (faces is { } known && sprites.Get(known.Top, tint) is { } top)
        {
            var side = known.Form == FaceForm.Cross ? top
                : sprites.Get(known.Side, known.SideOverlay is null ? tint : 0xFFFFFF, known.SideOverlay, tint) ?? top;
            var front = known.Front is { } named ? sprites.Get(named, tint) : null;
            return new Material(id, data, known.Form, top, side, Untextured: false,
                                Solid: known.Form == FaceForm.Cube && !detail, ground, boxes, known.Grain, front,
                                known.Facing);
        }
        var colour = sprites.Solid((uint)BlockPalette.PackedRgb(id, data));
        return new Material(id, data, FaceForm.Cube, colour, colour, Untextured: true, Solid: !detail, ground, boxes,
                            Grain.Up);
    }

    /// <summary>The height an eye stands at over <paramref name="x"/>, <paramref name="z"/>: a player's eye
    /// above the highest ground there with two blocks of room over it, or null for a column with none.</summary>
    public double? EyeAt(int x, int z) => StandingTop(x - _minX, z - _minZ) is { } top ? top + 1 + EyeHeight : null;

    /// <summary>The ground a player would stand on at <paramref name="x"/>, <paramref name="z"/> — its top
    /// block's height — or null for a column with none.</summary>
    public int? GroundAt(int x, int z) => StandingTop(x - _minX, z - _minZ);

    /// <summary>An eye at <paramref name="fromX"/>, <paramref name="fromZ"/> turned to look at the middle of
    /// whatever stands at <paramref name="atX"/>, <paramref name="atZ"/>, or tipped
    /// <paramref name="pitch"/> degrees down where that is given. It stands at <paramref name="eyeY"/> where
    /// that is given, on the ground where there is some, and otherwise hovers level with the thing's middle —
    /// the view from over the void beside a board.</summary>
    public EyeCamera Facing(int fromX, int fromZ, int atX, int atZ, double fov = 70, double? eyeY = null,
                            double? pitch = null)
    {
        var (_, _, middle) = Extent(atX - _minX, atZ - _minZ);
        var eye = eyeY ?? EyeAt(fromX, fromZ) ?? middle;
        var camera = Toward((fromX + 0.5, eye, fromZ + 0.5), (atX + 0.5, middle, atZ + 0.5), fov);
        return pitch is { } tipped ? camera with { Pitch = tipped } : camera;
    }

    /// <summary>
    /// A camera that sees what stands at <paramref name="atX"/>, <paramref name="atZ"/>, or null where no
    /// place near it does.
    ///
    /// <para>It stands on ground within <paramref name="nearest"/> to <paramref name="farthest"/> blocks of
    /// the thing — farther for a large one, so the thing fits — with room for a player over it, and it keeps
    /// only a place that sees the thing's middle, both flanks and its top with nothing solid in between and
    /// nothing solid in the first three blocks in front of the eye. Of those it takes the one standing on the
    /// ground the thing stands on, at about ten blocks.</para>
    ///
    /// <para>Where no ground sees it — a goal floating over the void, a thing walled in by its own hill — the
    /// eye hovers instead: it rises over the thing's top a few blocks at a time and takes the lowest place in
    /// the air that sees it the same way.</para>
    /// </summary>
    public EyeCamera? Frame(int atX, int atZ, int nearest = 7, int farthest = 14, double fov = 62)
    {
        int gx = atX - _minX, gz = atZ - _minZ;
        if (!Inside(gx, 0, gz)) return null;
        var (baseY, topY, middle) = Extent(gx, gz);
        var body = 0;
        for (var i = -6; i <= 6; i++)
            for (var j = -6; j <= 6; j++)
                if (StandingTop(gx + i, gz + j) is { } top && top > baseY + 1) body++;
        var reach = Math.Max(1.6, Math.Sqrt(body / Math.PI) + 0.5);
        var target = (gx + 0.5, middle, gz + 0.5);

        int closest = Math.Max(nearest, (int)reach + 4), furthest = Math.Max(farthest, (int)reach + 12);

        bool Sees((double X, double Y, double Z) eye, double ux, double uz)
        {
            double px = -uz, pz = ux;
            (double, double, double)[] sights =
            [
                target,
                (target.Item1 + px * 1.5, middle, target.Item3 + pz * 1.5),
                (target.Item1 - px * 1.5, middle, target.Item3 - pz * 1.5),
                (target.Item1, topY + 0.9, target.Item3),
            ];
            return !sights.Any(sight => Blocked(eye, sight, reach)) && !Crowded(eye, ux, uz);
        }

        (double Score, (double X, double Y, double Z) Eye)? best = null;
        for (var k = 0; k < 16; k++)
        {
            var angle = k * Math.PI / 8;
            double ux = Math.Cos(angle), uz = Math.Sin(angle);
            for (var distance = closest; distance <= furthest; distance++)
            {
                int cx = (int)Math.Floor(gx + ux * distance), cz = (int)Math.Floor(gz + uz * distance);
                if (StandingTop(cx, cz) is not { } ground) continue;
                (double X, double Y, double Z) eye = (cx + 0.5, ground + 1 + EyeHeight, cz + 0.5);
                if (!Sees(eye, ux, uz)) continue;
                var score = -Math.Abs(ground - baseY) * 1.5 - Math.Abs(distance - 10) * 0.4;
                if (best is null || score > best.Value.Score) best = (score, eye);
            }
        }

        for (var lift = 3; best is null && lift <= HoverHighest; lift += 4)
            for (var k = 0; k < 16; k++)
            {
                var angle = k * Math.PI / 8;
                double ux = Math.Cos(angle), uz = Math.Sin(angle);
                for (var distance = closest; distance <= furthest; distance++)
                {
                    (double X, double Y, double Z) eye = (gx + ux * distance + 0.5, topY + lift, gz + uz * distance + 0.5);
                    if (At((int)Math.Floor(eye.X), (int)Math.Floor(eye.Y), (int)Math.Floor(eye.Z)) is not null
                        || !Sees(eye, ux, uz)) continue;
                    var score = -Math.Abs(distance - 10) * 0.4;
                    if (best is null || score > best.Value.Score) best = (score, eye);
                }
            }

        if (best is not { Eye: var found }) return null;
        return Toward((found.X + _minX, found.Y, found.Z + _minZ), (atX + 0.5, middle, atZ + 0.5), fov);
    }

    private static EyeCamera Toward((double X, double Y, double Z) eye, (double X, double Y, double Z) at, double fov)
    {
        var yaw = Heading.YawTo(eye.X, eye.Z, at.X, at.Z);
        var pitch = Heading.PitchTo(eye.X, eye.Y, eye.Z, at.X, at.Y, at.Z);
        return new EyeCamera(eye.X, eye.Y, eye.Z, yaw, pitch, fov);
    }

    /// <summary>The ground a thing stands on, its top, and the height of its middle, over a column.</summary>
    private (int Base, int Top, double Middle) Extent(int gx, int gz)
    {
        var ring = new List<int>();
        for (var i = -4; i <= 4; i++)
            for (var j = -4; j <= 4; j++)
                if (StandingTop(gx + i, gz + j) is { } top) ring.Add(top);
        var near = new List<int>();
        for (var i = -1; i <= 1; i++)
            for (var j = -1; j <= 1; j++)
                if (StandingTop(gx + i, gz + j) is { } top) near.Add(top);
        ring.Sort();
        var baseY = ring.Count > 0 ? ring[ring.Count / 4] : 0;
        var topY = near.Count > 0 ? near.Max() : baseY + 2;
        return (baseY, topY, (baseY + topY) / 2.0 + 1);
    }

    private bool Inside(int gx, int y, int gz) =>
        gx >= 0 && gx < _width && y >= 0 && y < _height && gz >= 0 && gz < _depth;

    private Material? At(int gx, int y, int gz) =>
        Inside(gx, y, gz) && _cells[(gx * _height + y) * _depth + gz] is var slot and > 0 ? _materials[slot] : null;

    private bool Solid(int gx, int y, int gz) => At(gx, y, gz) is { Solid: true };

    /// <summary>The highest block a player could stand on in a column: ground rather than a tree's crown, with
    /// two cells clear above it.</summary>
    private int? StandingTop(int gx, int gz)
    {
        if (gx < 0 || gx >= _width || gz < 0 || gz >= _depth) return null;
        for (var y = _height - 3; y >= 0; y--)
            if (At(gx, y, gz) is { Ground: true })
                return !Solid(gx, y + 1, gz) && !Solid(gx, y + 2, gz) ? y : null;
        return null;
    }

    private bool Blocked((double X, double Y, double Z) from, (double X, double Y, double Z) to, double reach)
    {
        double dx = to.X - from.X, dy = to.Y - from.Y, dz = to.Z - from.Z;
        var length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        var steps = (int)(length * 4);
        for (var i = 1; i < steps; i++)
        {
            var along = (double)i / steps;
            if ((1 - along) * length < reach) break;
            if (Solid((int)Math.Floor(from.X + dx * along), (int)Math.Floor(from.Y + dy * along),
                      (int)Math.Floor(from.Z + dz * along))) return true;
        }
        return false;
    }

    /// <summary>Whether anything — a block, a leaf, a tall plant — stands in the first three blocks in front of
    /// the eye, where it would fill the frame.</summary>
    private bool Crowded((double X, double Y, double Z) eye, double ux, double uz)
    {
        for (var step = 1; step <= 3; step++)
            foreach (var lift in (double[])[-1.0, 0.0, 0.6])
                if (At((int)Math.Floor(eye.X - ux * step), (int)Math.Floor(eye.Y + lift),
                       (int)Math.Floor(eye.Z - uz * step)) is not null) return true;
        return false;
    }

    /// <summary>The picture <paramref name="camera"/> sees, <paramref name="pixelsWide"/> by
    /// <paramref name="pixelsHigh"/>, each pixel the mean of <paramref name="supersample"/>² rays.</summary>
    public EyePicture Draw(EyeCamera camera, int pixelsWide, int pixelsHigh, int supersample = 2)
    {
        var rgb = new byte[pixelsWide * pixelsHigh * 3];
        var hits = new int[_materials.Length];
        var sky = 0;
        var lens = Lens.Of(camera, pixelsWide, pixelsHigh);
        var origin = (X: camera.X - _minX, Y: camera.Y, Z: camera.Z - _minZ);
        var lockObject = new object();

        Parallel.For(0, pixelsHigh, row =>
        {
            var rowHits = new int[_materials.Length];
            var rowSky = 0;
            for (var column = 0; column < pixelsWide; column++)
            {
                double red = 0, green = 0, blue = 0;
                for (var sy = 0; sy < supersample; sy++)
                    for (var sx = 0; sx < supersample; sx++)
                    {
                        var (colour, slot, _) = Cast(origin,
                            lens.Ray(column + (sx + 0.5) / supersample, row + (sy + 0.5) / supersample));
                        red += (colour >> 16) & 0xFF; green += (colour >> 8) & 0xFF; blue += colour & 0xFF;
                        if (sx == 0 && sy == 0)
                        {
                            if (slot > 0) rowHits[slot]++; else rowSky++;
                        }
                    }
                var samples = supersample * supersample;
                var at = (row * pixelsWide + column) * 3;
                rgb[at] = (byte)(red / samples); rgb[at + 1] = (byte)(green / samples); rgb[at + 2] = (byte)(blue / samples);
            }
            lock (lockObject)
            {
                for (var i = 0; i < hits.Length; i++) hits[i] += rowHits[i];
                sky += rowSky;
            }
        });

        var total = (double)pixelsWide * pixelsHigh;
        var seen = new Dictionary<(int Id, int Data), int>();
        var untextured = 0;
        for (var slot = 1; slot < hits.Length; slot++)
        {
            if (hits[slot] == 0) continue;
            var material = _materials[slot];
            seen[(material.Id, material.Data)] = seen.GetValueOrDefault((material.Id, material.Data)) + hits[slot];
            if (material.Untextured) untextured += hits[slot];
        }
        return new EyePicture(pixelsWide, pixelsHigh, rgb,
            [.. seen.OrderByDescending(entry => entry.Value)
                    .Select(entry => new SeenBlock(entry.Key.Id, entry.Key.Data, entry.Value / total))],
            sky / total, untextured / total);
    }

    /// <summary>The block the ray through pixel <paramref name="px"/>, <paramref name="py"/> of a
    /// <paramref name="pixelsWide"/> × <paramref name="pixelsHigh"/> picture from <paramref name="camera"/>
    /// hits — the same ray <see cref="Draw"/> casts through that pixel's middle — or null for sky.</summary>
    public EyeHit? Pick(EyeCamera camera, int pixelsWide, int pixelsHigh, int px, int py)
    {
        var origin = (X: camera.X - _minX, Y: camera.Y, Z: camera.Z - _minZ);
        var (_, slot, cell) = Cast(origin, Lens.Of(camera, pixelsWide, pixelsHigh).Ray(px + 0.5, py + 0.5));
        if (slot == 0) return null;
        (int X, int Y, int Z)? ground = null;
        for (var y = cell.Y; y >= 0; y--)
            if (At(cell.X, y, cell.Z) is { Ground: true })
            {
                ground = (cell.X + _minX, y, cell.Z + _minZ);
                break;
            }
        return new EyeHit((cell.X + _minX, cell.Y, cell.Z + _minZ), ground);
    }

    /// <summary>The ground <paramref name="pixels"/>' rays hit in a <paramref name="pixelsWide"/> ×
    /// <paramref name="pixelsHigh"/> picture from <paramref name="camera"/>: each column once, at the highest
    /// block a ray hit in it. Ground hidden behind a hill is not in it, because no ray reached it, and a pixel of
    /// sky adds nothing but to <see cref="EyeArea.Sky"/>.</summary>
    public EyeArea Project(EyeCamera camera, int pixelsWide, int pixelsHigh, IReadOnlyList<(int X, int Y)> pixels)
    {
        var origin = (X: camera.X - _minX, Y: camera.Y, Z: camera.Z - _minZ);
        var lens = Lens.Of(camera, pixelsWide, pixelsHigh);
        var columns = new Dictionary<(int X, int Z), int>();
        var sky = 0;
        var lockObject = new object();
        Parallel.ForEach(System.Collections.Concurrent.Partitioner.Create(0, pixels.Count, 4096), range =>
        {
            var found = new Dictionary<(int X, int Z), int>();
            var missed = 0;
            for (var i = range.Item1; i < range.Item2; i++)
            {
                var (px, py) = pixels[i];
                var (_, slot, cell) = Cast(origin, lens.Ray(px + 0.5, py + 0.5));
                if (slot == 0) { missed++; continue; }
                var key = (cell.X + _minX, cell.Z + _minZ);
                if (!found.TryGetValue(key, out var y) || cell.Y > y) found[key] = cell.Y;
            }
            lock (lockObject)
            {
                sky += missed;
                foreach (var (key, y) in found)
                    if (!columns.TryGetValue(key, out var kept) || y > kept) columns[key] = y;
            }
        });
        return new EyeArea(columns, sky);
    }

    /// <summary>How a camera turns a pixel into a ray: its forward, right and up axes and the half-width of its
    /// field of view, for a picture of a given size.</summary>
    private readonly record struct Lens(
        (double X, double Y, double Z) Forward, (double X, double Y, double Z) Right, (double X, double Y, double Z) Up,
        double Half, int Wide, int High)
    {
        public static Lens Of(EyeCamera camera, int pixelsWide, int pixelsHigh)
        {
            var yaw = camera.Yaw * Math.PI / 180;
            var pitch = camera.Pitch * Math.PI / 180;
            var forward = (X: -Math.Sin(yaw) * Math.Cos(pitch), Y: -Math.Sin(pitch), Z: Math.Cos(yaw) * Math.Cos(pitch));
            var right = (X: -Math.Cos(yaw), Y: 0.0, Z: -Math.Sin(yaw));
            var up = (X: right.Y * forward.Z - right.Z * forward.Y,
                      Y: right.Z * forward.X - right.X * forward.Z,
                      Z: right.X * forward.Y - right.Y * forward.X);
            if (up.Y < 0) up = (-up.X, -up.Y, -up.Z);
            return new Lens(forward, right, up, Math.Tan(camera.Fov * Math.PI / 360), pixelsWide, pixelsHigh);
        }

        /// <summary>The unit ray through the point <paramref name="across"/>, <paramref name="down"/> pixels
        /// from the picture's top-left corner.</summary>
        public (double X, double Y, double Z) Ray(double across, double down)
        {
            var sideways = (across / Wide * 2 - 1) * Half;
            var lift = (1 - down / High * 2) * Half * High / Wide;
            var ray = (X: Forward.X + sideways * Right.X + lift * Up.X,
                       Y: Forward.Y + sideways * Right.Y + lift * Up.Y,
                       Z: Forward.Z + sideways * Right.Z + lift * Up.Z);
            var norm = Math.Sqrt(ray.X * ray.X + ray.Y * ray.Y + ray.Z * ray.Z);
            return (ray.X / norm, ray.Y / norm, ray.Z / norm);
        }
    }

    /// <summary>What one ray hits: the shaded colour, the material slot, 0 for sky, and the cell it hit in the
    /// scene's own coordinates.</summary>
    private (int Colour, int Slot, (int X, int Y, int Z) Cell) Cast((double X, double Y, double Z) origin,
                                                                   (double X, double Y, double Z) ray)
    {
        if (_width == 0) return (SkyRgb, 0, default);
        // Start where the ray enters the world's box, for an eye standing outside it.
        var entry = EnterBox(origin, ray);
        if (entry is not { } start) return (SkyRgb, 0, default);
        var position = (X: origin.X + ray.X * start, Y: origin.Y + ray.Y * start, Z: origin.Z + ray.Z * start);
        int cellX = Math.Clamp((int)Math.Floor(position.X), 0, _width - 1);
        int cellY = Math.Clamp((int)Math.Floor(position.Y), 0, _height - 1);
        int cellZ = Math.Clamp((int)Math.Floor(position.Z), 0, _depth - 1);
        int stepX = ray.X > 0 ? 1 : -1, stepY = ray.Y > 0 ? 1 : -1, stepZ = ray.Z > 0 ? 1 : -1;
        double deltaX = ray.X == 0 ? double.MaxValue : Math.Abs(1 / ray.X);
        double deltaY = ray.Y == 0 ? double.MaxValue : Math.Abs(1 / ray.Y);
        double deltaZ = ray.Z == 0 ? double.MaxValue : Math.Abs(1 / ray.Z);
        double nextX = ray.X == 0 ? double.MaxValue : start + (ray.X > 0 ? cellX + 1 - position.X : position.X - cellX) * deltaX;
        double nextY = ray.Y == 0 ? double.MaxValue : start + (ray.Y > 0 ? cellY + 1 - position.Y : position.Y - cellY) * deltaY;
        double nextZ = ray.Z == 0 ? double.MaxValue : start + (ray.Z > 0 ? cellZ + 1 - position.Z : position.Z - cellZ) * deltaZ;
        var travelled = start;
        var axis = start > 0 ? EntryAxis(origin, ray, start) : -1;

        while (travelled - start < FarEnough)
        {
            var slot = _cells[(cellX * _height + cellY) * _depth + cellZ];
            if (slot > 0 && axis >= 0 && Hit(_materials[slot], origin, ray, travelled, axis, cellX, cellY, cellZ,
                    Math.Min(nextX, Math.Min(nextY, nextZ))) is { } colour)
                return (colour, slot, (cellX, cellY, cellZ));

            if (nextX < nextY && nextX < nextZ) { cellX += stepX; travelled = nextX; nextX += deltaX; axis = 0; }
            else if (nextY < nextZ) { cellY += stepY; travelled = nextY; nextY += deltaY; axis = 1; }
            else { cellZ += stepZ; travelled = nextZ; nextZ += deltaZ; axis = 2; }
            if (cellX < 0 || cellX >= _width || cellY < 0 || cellY >= _height || cellZ < 0 || cellZ >= _depth)
                return (SkyRgb, 0, default);
        }
        return (SkyRgb, 0, default);
    }

    /// <summary>How far along the ray it enters the world's box, 0 for an eye already inside, or null for a
    /// ray that never does.</summary>
    private double? EnterBox((double X, double Y, double Z) origin, (double X, double Y, double Z) ray)
    {
        double near = 0, far = double.MaxValue;
        foreach (var (start, direction, size) in (ReadOnlySpan<(double, double, int)>)
                 [(origin.X, ray.X, _width), (origin.Y, ray.Y, _height), (origin.Z, ray.Z, _depth)])
        {
            if (Math.Abs(direction) < 1e-12)
            {
                if (start < 0 || start >= size) return null;
                continue;
            }
            var first = (0 - start) / direction;
            var second = (size - start) / direction;
            near = Math.Max(near, Math.Min(first, second));
            far = Math.Min(far, Math.Max(first, second));
        }
        return near <= far ? near + (near > 0 ? 1e-6 : 0) : null;
    }

    private static int EntryAxis((double X, double Y, double Z) origin, (double X, double Y, double Z) ray, double at)
    {
        var point = (origin.X + ray.X * at, origin.Y + ray.Y * at, origin.Z + ray.Z * at);
        double fx = Math.Abs(point.Item1 - Math.Round(point.Item1)), fy = Math.Abs(point.Item2 - Math.Round(point.Item2));
        var fz = Math.Abs(point.Item3 - Math.Round(point.Item3));
        return fx <= fy && fx <= fz ? 0 : fy <= fz ? 1 : 2;
    }

    /// <summary>The colour a ray takes from the cell it has just entered, or null where it passes through.</summary>
    private static int? Hit(Material material, (double X, double Y, double Z) origin, (double X, double Y, double Z) ray,
        double entered, int axis, int cellX, int cellY, int cellZ, double leaves)
    {
        if (material.Form == FaceForm.Cross) return Plant(material, origin, ray, entered, leaves, cellX, cellY, cellZ);
        if (material.Boxes is { } boxes) return Shaped(material, boxes, origin, ray, entered, axis, leaves, cellX, cellY, cellZ);

        var point = (X: origin.X + ray.X * entered - cellX, Y: origin.Y + ray.Y * entered - cellY,
                     Z: origin.Z + ray.Z * entered - cellZ);
        var (sprite, u, v, shade) = Face(material, axis, point, ray);
        if (sprite is null) return null;
        var texel = sprite.At(u, v);
        if (texel >> 24 < 128) return null;
        return Shade(texel, shade);
    }

    /// <summary>The nearest face of a box the block fills that the ray meets inside the cell, in the sprite of
    /// that face at the point it is met — so a slab's side shows the half of the sprite it stands in, as the
    /// game draws it.</summary>
    private static int? Shaped(Material material, CellBox[] boxes, (double X, double Y, double Z) origin,
        (double X, double Y, double Z) ray, double entered, int axis, double leaves, int cellX, int cellY, int cellZ)
    {
        var local = (X: origin.X - cellX, Y: origin.Y - cellY, Z: origin.Z - cellZ);
        double nearest = double.MaxValue;
        int? colour = null;
        foreach (var box in boxes)
        {
            double near = entered, far = leaves;
            var face = axis;
            var missed = false;
            foreach (var (start, direction, low, high, which) in (ReadOnlySpan<(double, double, double, double, int)>)
                     [(local.X, ray.X, box.MinX, box.MaxX, 0), (local.Y, ray.Y, box.MinY, box.MaxY, 1),
                      (local.Z, ray.Z, box.MinZ, box.MaxZ, 2)])
            {
                if (Math.Abs(direction) < 1e-12)
                {
                    var at = start + direction * entered;
                    if (at < low || at > high) missed = true;
                    continue;
                }
                double first = (low - start) / direction, second = (high - start) / direction;
                if (Math.Min(first, second) > near) { near = Math.Min(first, second); face = which; }
                far = Math.Min(far, Math.Max(first, second));
            }
            if (missed || near > far || near >= nearest) continue;
            var point = (X: local.X + ray.X * near, Y: local.Y + ray.Y * near, Z: local.Z + ray.Z * near);
            var (sprite, u, v, shade) = Face(material, face, point, ray);
            if (sprite is null) continue;
            var texel = sprite.At(Math.Clamp(u, 0, 0.9999), Math.Clamp(v, 0, 0.9999));
            if (texel >> 24 < 128) continue;
            nearest = near;
            colour = Shade(texel, shade);
        }
        return colour;
    }

    /// <summary>The sprite the face across <paramref name="face"/>'s axis shows, where on it a point in the cell
    /// falls, and the shade a face pointing that way takes. The end sprite goes on the two faces the grain runs
    /// out of, and on a block lying down the side sprite is turned so its grain runs along the block — the bark
    /// of a beam runs the way the beam does. A block with a front wears it on the side it looks toward.</summary>
    private static (BlockSprite? Sprite, double U, double V, double Shade) Face(
        Material material, int face, (double X, double Y, double Z) point, (double X, double Y, double Z) ray)
    {
        var shade = face switch { 1 => ray.Y < 0 ? 1.0 : 0.5, 0 => 0.6, _ => 0.8 };
        if (material.Front is { } front && face != 1)
        {
            // A ray travelling toward +x meets the face on a cell's west side, and one toward +z its north side.
            var side = face == 0 ? ray.X > 0 ? RoomEdge.NegX : RoomEdge.PosX : ray.Z > 0 ? RoomEdge.NegZ : RoomEdge.PosZ;
            if (side == material.Facing)
                return face == 0 ? (front, point.Z, 1 - point.Y, shade) : (front, point.X, 1 - point.Y, shade);
        }
        return (material.Grain, face) switch
        {
            (Grain.AlongX, 0) => (material.Top, point.Z, 1 - point.Y, shade),
            (Grain.AlongX, 1) => (material.Side, point.Z, point.X, shade),
            (Grain.AlongX, _) => (material.Side, 1 - point.Y, point.X, shade),
            (Grain.AlongZ, 2) => (material.Top, point.X, 1 - point.Y, shade),
            (Grain.AlongZ, 1) => (material.Side, point.X, point.Z, shade),
            (Grain.AlongZ, _) => (material.Side, 1 - point.Y, point.Z, shade),
            (_, 1) => (material.Top, point.X, point.Z, shade),
            (_, 0) => (material.Side, point.Z, 1 - point.Y, shade),
            _ => (material.Side, point.X, 1 - point.Y, shade),
        };
    }

    /// <summary>Two quads crossing through the cell's centre on its diagonals; the nearer opaque texel wins.</summary>
    private static int? Plant(Material material, (double X, double Y, double Z) origin, (double X, double Y, double Z) ray,
        double entered, double leaves, int cellX, int cellY, int cellZ)
    {
        if (material.Top is not { } sprite) return null;
        var local = (X: origin.X + ray.X * entered - cellX, Y: origin.Y + ray.Y * entered - cellY,
                     Z: origin.Z + ray.Z * entered - cellZ);
        double? nearest = null;
        uint colour = 0;
        foreach (var (sign, offset) in (ReadOnlySpan<(int, double)>)[(1, 0.0), (-1, 1.0)])
        {
            var denominator = ray.X - sign * ray.Z;
            if (Math.Abs(denominator) < 1e-12) continue;
            var along = (offset - (local.X - sign * local.Z)) / denominator;
            if (along < 0 || along > leaves - entered) continue;
            double qx = local.X + ray.X * along, qy = local.Y + ray.Y * along;
            if (qx < 0 || qx > 1 || qy < 0 || qy > 1) continue;
            var texel = sprite.At(qx, 1 - qy);
            if (texel >> 24 < 128 || (nearest is { } seen && along >= seen)) continue;
            nearest = along; colour = texel;
        }
        return nearest is null ? null : Shade(colour, 0.9);
    }

    private static int Shade(uint texel, double shade) =>
        ((int)(((texel >> 16) & 0xFF) * shade) << 16) | ((int)(((texel >> 8) & 0xFF) * shade) << 8)
        | (int)((texel & 0xFF) * shade);

    /// <summary>Each sprite tinted once per colour, and in flat mode reduced to its mean.</summary>
    private sealed class SpriteCache(BlockTextureSet textures, bool flat)
    {
        private readonly Dictionary<(string Name, uint Tint, string? Overlay, uint OverlayTint), BlockSprite?> _made = [];

        public BlockSprite? Get(string name, uint tint, string? overlay = null, uint overlayTint = 0xFFFFFF)
        {
            if (_made.TryGetValue((name, tint, overlay, overlayTint), out var made)) return made;
            BlockSprite? sprite = null;
            if (textures.Get(name) is { } source)
            {
                var rgba = Tinted(source.Rgba, tint);
                if (overlay is not null && textures.Get(overlay) is { Size: var size } mask && size == source.Size)
                    Lay(rgba, Tinted(mask.Rgba, overlayTint));
                sprite = new BlockSprite(source.Size, flat ? Mean(rgba) : rgba);
            }
            return _made[(name, tint, overlay, overlayTint)] = sprite;
        }

        public BlockSprite Solid(uint rgb) => new(1, [(byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255]);

        private static byte[] Tinted(byte[] rgba, uint tint)
        {
            var copy = (byte[])rgba.Clone();
            if (tint == 0xFFFFFF) return copy;
            int red = (int)(tint >> 16) & 0xFF, green = (int)(tint >> 8) & 0xFF, blue = (int)tint & 0xFF;
            for (var i = 0; i < copy.Length; i += 4)
            {
                copy[i] = (byte)(copy[i] * red / 255);
                copy[i + 1] = (byte)(copy[i + 1] * green / 255);
                copy[i + 2] = (byte)(copy[i + 2] * blue / 255);
            }
            return copy;
        }

        private static void Lay(byte[] under, byte[] over)
        {
            for (var i = 0; i < under.Length; i += 4)
            {
                var weight = over[i + 3] / 255.0;
                for (var channel = 0; channel < 3; channel++)
                    under[i + channel] = (byte)(under[i + channel] * (1 - weight) + over[i + channel] * weight);
            }
        }

        /// <summary>Every opaque texel replaced by the mean of the opaque ones; transparency is kept, so a leaf
        /// still lets the ray through where it did.</summary>
        private static byte[] Mean(byte[] rgba)
        {
            long red = 0, green = 0, blue = 0, count = 0;
            for (var i = 0; i < rgba.Length; i += 4)
            {
                if (rgba[i + 3] < 128) continue;
                red += rgba[i]; green += rgba[i + 1]; blue += rgba[i + 2]; count++;
            }
            if (count == 0) return rgba;
            var flatRgba = (byte[])rgba.Clone();
            for (var i = 0; i < flatRgba.Length; i += 4)
            {
                if (flatRgba[i + 3] < 128) continue;
                flatRgba[i] = (byte)(red / count); flatRgba[i + 1] = (byte)(green / count); flatRgba[i + 2] = (byte)(blue / count);
            }
            return flatRgba;
        }
    }
}
