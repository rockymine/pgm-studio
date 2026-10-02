using PgmStudio.Domain;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Render;

/// <summary>A box inside one block's cell, in block units from the cell's minimum corner.</summary>
public readonly record struct CellBox(double MinX, double MinY, double MinZ, double MaxX, double MaxY, double MaxZ);

/// <summary>The sides of its cell a fence, a pane or a wall reaches out to meet a neighbour on.</summary>
[Flags]
public enum Joins
{
    None = 0,
    North = 1,
    South = 2,
    West = 4,
    East = 8,
}

/// <summary>Which neighbours a block reaches out to, for the three kinds whose drawing depends on them.</summary>
public enum JoinKind
{
    None,
    Fence,
    Pane,
    Wall,
}

/// <summary>
/// The part of its cell a block fills, for a block that fills less than all of it: a slab's half, a stair's
/// half and step, a fence's post and rails, a pane's or an iron bar's sheet, a wall's post and arms. A
/// stair's corner join with the stair beside it is not drawn — each stair is its straight shape. A carpet, a
/// redstone wire, a lily pad, a rail and a pressure plate are a sheet on the floor of their cell, a ladder and
/// a vine a sheet against the side that holds them up, and a chest the box inset a sixteenth from each side
/// of its cell. A door, a trapdoor, a sign, a button and a lever are the boards the game draws them as,
/// turned by their data; a hopper, a cauldron, an anvil, a bed, a cake, a snow layer, an enchanting table, an
/// end portal frame, a daylight sensor, a flower pot and a skull are the boxes they stand as. A lever's
/// handle, an end portal frame's eye and a cauldron's water are not drawn.
/// </summary>
public static class BlockShape
{
    private const double RailLow = 0.375, RailLowTop = 0.5625, RailHigh = 0.75, RailHighTop = 0.9375;
    private const double WallTop = 0.8125;

    /// <summary>How thick a sheet is: one texel of a sixteen-texel sprite.</summary>
    private const double SheetThickness = 1.0 / 16;

    private const double ChestInset = 1.0 / 16, ChestTop = 14.0 / 16;

    /// <summary>The boxes <paramref name="id"/>:<paramref name="data"/> fills, joined on
    /// <paramref name="joins"/>, or null for a block that fills its whole cell.</summary>
    public static CellBox[]? Of(int id, int data, Joins joins = Joins.None) => id switch
    {
        44 or 126 or 182 => (data & 8) != 0 ? [new(0, 0.5, 0, 1, 1, 1)] : [new(0, 0, 0, 1, 0.5, 1)],
        53 or 67 or 108 or 109 or 114 or 128 or 134 or 135 or 136 or 156 or 163 or 164 or 180 => Stair(data),
        Blocks.RedstoneWire or 111 or 171 => [new(0, 0, 0, 1, SheetThickness, 1)],
        Blocks.Ladder => [Against((BlockGeometry.Front(data) ?? RoomEdge.NegZ).Opposite())],
        Blocks.Vine => Vine(data),
        Blocks.Chest or 130 or 146 =>
            [new(ChestInset, 0, ChestInset, 1 - ChestInset, ChestTop, 1 - ChestInset)],
        63 => StandingSign(data),
        68 => [Flat(WallSignSide(data), 2.0 / 16, 0.25, 0.75)],
        69 => [Lever(data)],
        77 or 143 => [OnFace(data & 7, 6.0 / 16, 4.0 / 16, (data & 8) != 0 ? SheetThickness : 2.0 / 16)],
        70 or 72 or 147 or 148 => [Plate(data != 0)],
        27 or 28 or 66 or 157 => [new(0, 0, 0, 1, SheetThickness, 1)],
        96 or 167 => Trapdoor(data),
        _ when IsDoor(id) => [Door(data)],
        140 => [Sixteenths(5, 0, 5, 11, 6, 11)],
        118 => Cauldron(),
        154 => Hopper(data),
        151 or 178 => [Sixteenths(0, 0, 0, 16, 6, 16)],
        145 => Anvil(data),
        116 => [Sixteenths(0, 0, 0, 16, 12, 16)],
        120 => [Sixteenths(0, 0, 0, 16, 13, 16)],
        92 => [Sixteenths(1 + 2 * Math.Min(data & 7, 6), 0, 1, 15, 8, 15)],
        26 => [Sixteenths(0, 0, 0, 16, 9, 16)],
        78 when (data & 7) != 7 => [new(0, 0, 0, 1, ((data & 7) + 1) / 8.0, 1)],
        144 => [Skull(data)],
        _ => Joining(id) switch
        {
            JoinKind.Fence => Fence(joins),
            JoinKind.Pane => Pane(joins),
            JoinKind.Wall => Wall(joins),
            _ => id is 107 or (>= 183 and <= 187) ? Gate(data) : null,
        },
    };

    /// <summary>Whether every box is a sheet no thicker than <see cref="SheetThickness"/> — a carpet, a wire, a
    /// ladder: drawn, but nothing to stand on or to block a line of sight.</summary>
    public static bool Sheet(CellBox[]? boxes) =>
        boxes is { Length: > 0 } && boxes.All(box => Math.Min(box.MaxX - box.MinX,
            Math.Min(box.MaxY - box.MinY, box.MaxZ - box.MinZ)) <= SheetThickness + 1e-9);

    /// <summary>Whether <paramref name="id"/> is a block a player walks through or stands beside rather than on —
    /// a door, a trapdoor, a sign, a button, a lever, a plate, a rail or a flower pot: drawn, but neither
    /// ground nor in the way of a line of sight.</summary>
    public static bool Detail(int id) =>
        id is 63 or 66 or 68 or 69 or 70 or 72 or 77 or 96 or 140 or 143 or 147 or 148 or 157 or 167 or 27 or 28
        || IsDoor(id);

    /// <summary>Whether <paramref name="id"/> is a wooden or iron door, whose two halves each name only part of
    /// what it looks like.</summary>
    public static bool IsDoor(int id) => id is 64 or 71 or (>= 193 and <= 197);

    /// <summary>
    /// A door half's data with what the other half holds folded in: the facing and open bits of the lower
    /// half in bits 0 to 2, the upper-half flag in bit 3 as stored, and the hinge side — kept in the upper
    /// half — in bit 4. <paramref name="data"/> is the half being drawn, <paramref name="otherData"/> the half
    /// above or below it, or 0 where there is none.
    /// </summary>
    public static int DoorData(int data, int otherData)
    {
        var upper = (data & 8) != 0;
        var lower = upper ? otherData : data;
        var hinge = upper ? data : otherData;
        return (lower & 7) | (data & 8) | ((hinge & 1) << 4);
    }

    /// <summary>Which neighbours <paramref name="id"/> reaches out to, if its shape depends on them.</summary>
    public static JoinKind Joining(int id) => id switch
    {
        85 or 113 or (>= 188 and <= 192) => JoinKind.Fence,
        101 or 102 or 160 => JoinKind.Pane,
        139 => JoinKind.Wall,
        _ => JoinKind.None,
    };

    /// <summary>Whether a block of <paramref name="kind"/> reaches out to a neighbour of
    /// <paramref name="neighbourId"/>: one of its own kind, a gate for a fence or a wall, glass for a pane, or
    /// any block that fills its whole cell and is not leaves or glass.</summary>
    public static bool Meets(JoinKind kind, int neighbourId, bool neighbourFillsCell)
    {
        if (Joining(neighbourId) == kind) return true;
        var gate = neighbourId is 107 or (>= 183 and <= 187);
        var glass = neighbourId is 20 or 95;
        return kind switch
        {
            JoinKind.Fence or JoinKind.Wall when gate => true,
            JoinKind.Pane when glass => true,
            JoinKind.None => false,
            _ => neighbourFillsCell && !glass && neighbourId is not (18 or 161),
        };
    }

    /// <summary>A stair: a half slab along its base and a step on the side it climbs toward, both turned
    /// over for one set upside down. Its low two bits name that side — east, west, south, north.</summary>
    private static CellBox[] Stair(int data)
    {
        var upsideDown = (data & 4) != 0;
        double baseLow = upsideDown ? 0.5 : 0, stepLow = upsideDown ? 0 : 0.5;
        var step = (data & 3) switch
        {
            0 => new CellBox(0.5, stepLow, 0, 1, stepLow + 0.5, 1),
            1 => new CellBox(0, stepLow, 0, 0.5, stepLow + 0.5, 1),
            2 => new CellBox(0, stepLow, 0.5, 1, stepLow + 0.5, 1),
            _ => new CellBox(0, stepLow, 0, 1, stepLow + 0.5, 0.5),
        };
        return [new(0, baseLow, 0, 1, baseLow + 0.5, 1), step];
    }

    /// <summary>A sheet against the side <paramref name="edge"/> of its cell.</summary>
    private static CellBox Against(RoomEdge edge) => Flat(edge, SheetThickness);

    /// <summary>A board <paramref name="thickness"/> thick against the side <paramref name="edge"/> of its
    /// cell, from <paramref name="low"/> to <paramref name="high"/> in height.</summary>
    private static CellBox Flat(RoomEdge edge, double thickness, double low = 0, double high = 1) => edge switch
    {
        RoomEdge.NegZ => new CellBox(0, low, 0, 1, high, thickness),
        RoomEdge.PosZ => new CellBox(0, low, 1 - thickness, 1, high, 1),
        RoomEdge.NegX => new CellBox(0, low, 0, thickness, high, 1),
        _ => new CellBox(1 - thickness, low, 0, 1, high, 1),
    };

    /// <summary>A box named in sixteenths of the cell, the unit the game's own models are drawn in.</summary>
    private static CellBox Sixteenths(double minX, double minY, double minZ, double maxX, double maxY, double maxZ) =>
        new(minX / 16, minY / 16, minZ / 16, maxX / 16, maxY / 16, maxZ / 16);

    /// <summary>A box <paramref name="wide"/> by <paramref name="tall"/> by <paramref name="deep"/> on the
    /// face of its cell a button or lever is attached to. <paramref name="facing"/> is the game's: 1 east, 2
    /// west, 3 south and 4 north on the wall opposite, 5 on the floor, 0 on the ceiling. On the floor or the
    /// ceiling <paramref name="tall"/> runs along z, or along x where <paramref name="longAlongX"/>.</summary>
    private static CellBox OnFace(int facing, double wide, double tall, double deep, bool longAlongX = false)
    {
        double acrossLow = (1 - wide) / 2, acrossHigh = 1 - acrossLow;
        double upLow = (1 - tall) / 2, upHigh = 1 - upLow;
        return facing switch
        {
            1 => new CellBox(0, upLow, acrossLow, deep, upHigh, acrossHigh),
            2 => new CellBox(1 - deep, upLow, acrossLow, 1, upHigh, acrossHigh),
            3 => new CellBox(acrossLow, upLow, 0, acrossHigh, upHigh, deep),
            4 => new CellBox(acrossLow, upLow, 1 - deep, acrossHigh, upHigh, 1),
            0 when longAlongX => new CellBox(upLow, 1 - deep, acrossLow, upHigh, 1, acrossHigh),
            0 => new CellBox(acrossLow, 1 - deep, upLow, acrossHigh, 1, upHigh),
            _ when longAlongX => new CellBox(upLow, 0, acrossLow, upHigh, deep, acrossHigh),
            _ => new CellBox(acrossLow, 0, upLow, acrossHigh, deep, upHigh),
        };
    }

    /// <summary>A lever's base: its low three bits name the face it is attached to, and for the two floor
    /// and two ceiling forms which way the base runs.</summary>
    private static CellBox Lever(int data) => (data & 7) switch
    {
        0 => OnFace(0, 6.0 / 16, 8.0 / 16, 3.0 / 16, longAlongX: true),
        7 => OnFace(0, 6.0 / 16, 8.0 / 16, 3.0 / 16),
        5 => OnFace(5, 6.0 / 16, 8.0 / 16, 3.0 / 16),
        6 => OnFace(5, 6.0 / 16, 8.0 / 16, 3.0 / 16, longAlongX: true),
        var wall => OnFace(wall, 6.0 / 16, 8.0 / 16, 3.0 / 16),
    };

    /// <summary>A pressure plate, fourteen texels square, a texel thick and half that once pressed.</summary>
    private static CellBox Plate(bool pressed) =>
        new(1.0 / 16, 0, 1.0 / 16, 15.0 / 16, pressed ? 1.0 / 32 : SheetThickness, 15.0 / 16);

    /// <summary>The side of its cell a wall sign hangs against: its data names the way it faces, 2 north, 3
    /// south, 4 west, 5 east, so the wall behind it is on the other side.</summary>
    private static RoomEdge WallSignSide(int data) => (data & 7) switch
    {
        2 => RoomEdge.PosZ,
        3 => RoomEdge.NegZ,
        4 => RoomEdge.PosX,
        _ => RoomEdge.NegX,
    };

    /// <summary>A standing sign: a post under a board, the board turned to the nearest of the four axes its
    /// sixteen rotations point along — 0 south, 4 west, 8 north, 12 east.</summary>
    private static CellBox[] StandingSign(int data)
    {
        var acrossX = (((data & 15) + 2) / 4 & 1) == 0;
        var board = acrossX ? Sixteenths(0, 8, 7, 16, 16, 9) : Sixteenths(7, 8, 0, 9, 16, 16);
        return [Sixteenths(7, 0, 7, 9, 8, 9), board];
    }

    /// <summary>A trapdoor: closed, a three-texel slab at the bottom of its cell or the top for an upper one,
    /// bit 3; open, bit 2, a sheet against the side its low two bits name — 0 south, 1 north, 2 east, 3 west.</summary>
    private static CellBox[] Trapdoor(int data)
    {
        const double thickness = 3.0 / 16;
        if ((data & 4) == 0)
            return [(data & 8) != 0 ? new CellBox(0, 1 - thickness, 0, 1, 1, 1) : new CellBox(0, 0, 0, 1, thickness, 1)];
        return [Flat((data & 3) switch
        {
            0 => RoomEdge.PosZ,
            1 => RoomEdge.NegZ,
            2 => RoomEdge.PosX,
            _ => RoomEdge.NegX,
        }, thickness)];
    }

    /// <summary>A door's three-texel panel, from data as <see cref="DoorData"/> resolves it. Closed, the low
    /// two bits name the side of the cell it stands on — 0 west, 1 north, 2 east, 3 south; open, it swings a
    /// quarter turn about the hinge side the upper half holds.</summary>
    private static CellBox Door(int data)
    {
        var open = (data & 4) != 0;
        var hingeRight = (data & 16) != 0;
        return Flat((data & 3) switch
        {
            0 => open ? hingeRight ? RoomEdge.PosZ : RoomEdge.NegZ : RoomEdge.NegX,
            1 => open ? hingeRight ? RoomEdge.NegX : RoomEdge.PosX : RoomEdge.NegZ,
            2 => open ? hingeRight ? RoomEdge.NegZ : RoomEdge.PosZ : RoomEdge.PosX,
            _ => open ? hingeRight ? RoomEdge.PosX : RoomEdge.NegX : RoomEdge.PosZ,
        }, 3.0 / 16);
    }

    /// <summary>A cauldron: four walls two texels thick over a floor, on four legs.</summary>
    private static CellBox[] Cauldron() =>
    [
        Sixteenths(0, 3, 0, 16, 6, 16),
        Sixteenths(0, 6, 0, 16, 16, 2), Sixteenths(0, 6, 14, 16, 16, 16),
        Sixteenths(0, 6, 2, 2, 16, 14), Sixteenths(14, 6, 2, 16, 16, 14),
        Sixteenths(0, 0, 0, 4, 3, 4), Sixteenths(12, 0, 0, 16, 3, 4),
        Sixteenths(0, 0, 12, 4, 3, 16), Sixteenths(12, 0, 12, 16, 3, 16),
    ];

    /// <summary>A hopper: a bowl over a neck, and a spout down or out of the side its low three bits name.</summary>
    private static CellBox[] Hopper(int data)
    {
        var spout = (data & 7) switch
        {
            2 => Sixteenths(6, 4, 0, 10, 8, 4),
            3 => Sixteenths(6, 4, 12, 10, 8, 16),
            4 => Sixteenths(0, 4, 6, 4, 8, 10),
            5 => Sixteenths(12, 4, 6, 16, 8, 10),
            _ => Sixteenths(6, 0, 6, 10, 4, 10),
        };
        return [Sixteenths(0, 10, 0, 16, 16, 16), Sixteenths(4, 4, 4, 12, 10, 12), spout];
    }

    /// <summary>An anvil: base, waist, neck and a long top, the top running along z for an even facing bit
    /// and along x for an odd one.</summary>
    private static CellBox[] Anvil(int data)
    {
        CellBox[] alongX =
        [
            Sixteenths(2, 0, 2, 14, 4, 14), Sixteenths(4, 4, 3, 12, 5, 13),
            Sixteenths(6, 5, 4, 10, 10, 12), Sixteenths(0, 10, 3, 16, 16, 13),
        ];
        return (data & 1) != 0
            ? alongX
            : [.. alongX.Select(box => new CellBox(box.MinZ, box.MinY, box.MinX, box.MaxZ, box.MaxY, box.MaxX))];
    }

    /// <summary>A skull: eight texels cube, on the floor, or against the wall its data names (2 north, 3 south,
    /// 4 west, 5 east) halfway up it.</summary>
    private static CellBox Skull(int data) => (data & 7) switch
    {
        2 => Sixteenths(4, 4, 8, 12, 12, 16),
        3 => Sixteenths(4, 4, 0, 12, 12, 8),
        4 => Sixteenths(8, 4, 4, 16, 12, 12),
        5 => Sixteenths(0, 4, 4, 8, 12, 12),
        _ => Sixteenths(4, 0, 4, 12, 8, 12),
    };

    /// <summary>A vine is a sheet against each side it clings to, or under the ceiling of its cell where it
    /// hangs from the block above.</summary>
    private static CellBox[] Vine(int data)
    {
        CellBox[] sides = [.. BlockGeometry.ClingsTo(data).Select(Against)];
        return sides.Length > 0 ? sides : [new(0, 1 - SheetThickness, 0, 1, 1, 1)];
    }

    private static CellBox[] Fence(Joins joins)
    {
        var boxes = new List<CellBox> { new(0.375, 0, 0.375, 0.625, 1, 0.625) };
        foreach (var side in Sides(joins))
        {
            boxes.Add(Arm(side, 0.4375, RailLow, RailLowTop, reach: 0.375));
            boxes.Add(Arm(side, 0.4375, RailHigh, RailHighTop, reach: 0.375));
        }
        return [.. boxes];
    }

    /// <summary>A pane is a sheet to each side it meets; one meeting nothing crosses its whole cell both
    /// ways.</summary>
    private static CellBox[] Pane(Joins joins)
    {
        if (joins == Joins.None) joins = Joins.North | Joins.South | Joins.West | Joins.East;
        return [.. Sides(joins).Select(side => Arm(side, 0.4375, 0, 1, reach: 0.4375))];
    }

    /// <summary>A wall is a post with an arm to each side it meets, and no post where it runs straight
    /// through.</summary>
    private static CellBox[] Wall(Joins joins)
    {
        var straight = joins is (Joins.North | Joins.South) or (Joins.West | Joins.East);
        var boxes = Sides(joins).Select(side => Arm(side, 0.3125, 0, WallTop, reach: 0.5)).ToList();
        if (!straight) boxes.Add(new CellBox(0.25, 0, 0.25, 0.75, 1, 0.75));
        return [.. boxes];
    }

    /// <summary>A closed gate is two rails across its cell between two short posts; its low two bits name the
    /// way it faces — south, west, north, east — and the rails run across that.</summary>
    private static CellBox[] Gate(int data)
    {
        var alongX = (data & 1) == 0;
        CellBox Across(double low, double high, double from, double to) => alongX
            ? new CellBox(from, low, 0.4375, to, high, 0.5625)
            : new CellBox(0.4375, low, from, 0.5625, high, to);
        CellBox[] posts = [Across(0.3125, 1, 0, 0.125), Across(0.3125, 1, 0.875, 1)];
        if ((data & 4) != 0) return posts;
        return [.. posts, Across(RailLow, RailLowTop, 0, 1), Across(RailHigh, RailHighTop, 0, 1)];
    }

    private static IEnumerable<Joins> Sides(Joins joins) =>
        new[] { Joins.North, Joins.South, Joins.West, Joins.East }.Where(side => joins.HasFlag(side));

    /// <summary>A bar on the cell's centre line, standing <paramref name="inset"/> in from both of the edges
    /// it runs between, from the edge on <paramref name="side"/> to <paramref name="reach"/> short of the far
    /// one.</summary>
    private static CellBox Arm(Joins side, double inset, double low, double high, double reach)
    {
        double narrow = inset, wide = 1 - inset;
        return side switch
        {
            Joins.North => new CellBox(narrow, low, 0, wide, high, 1 - reach),
            Joins.South => new CellBox(narrow, low, reach, wide, high, 1),
            Joins.West => new CellBox(0, low, narrow, 1 - reach, high, wide),
            _ => new CellBox(reach, low, narrow, 1, high, wide),
        };
    }
}
