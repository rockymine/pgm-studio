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
/// half and step, a fence's post and rails, a pane's sheet, a wall's post and arms. A stair's corner join
/// with the stair beside it is not drawn — each stair is its straight shape. A carpet, a redstone wire and a
/// lily pad are a sheet on the floor of their cell, a ladder and a vine a sheet against the side that holds
/// them up, and a chest the box inset a sixteenth from each side of its cell.
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

    /// <summary>Which neighbours <paramref name="id"/> reaches out to, if its shape depends on them.</summary>
    public static JoinKind Joining(int id) => id switch
    {
        85 or 113 or (>= 188 and <= 192) => JoinKind.Fence,
        102 or 160 => JoinKind.Pane,
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
    private static CellBox Against(RoomEdge edge) => edge switch
    {
        RoomEdge.NegZ => new CellBox(0, 0, 0, 1, 1, SheetThickness),
        RoomEdge.PosZ => new CellBox(0, 0, 1 - SheetThickness, 1, 1, 1),
        RoomEdge.NegX => new CellBox(0, 0, 0, SheetThickness, 1, 1),
        _ => new CellBox(1 - SheetThickness, 0, 0, 1, 1, 1),
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
