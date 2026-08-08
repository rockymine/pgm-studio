using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Pgm.Plan;

/// <summary>
/// Compiles a <see cref="PlanModel"/> one-way into the pair the downstream pipeline consumes: a
/// <see cref="SketchLayout"/> (terrain — land-connected pieces unioned into rectilinear shapes, grouped into
/// mirror islands) and a <see cref="MapIntent"/> (teams/spawns/wools/build fanned from the authored team-0
/// unit by symmetry). Pure + deterministic: the same plan compiles to the same pair on the server and in the
/// editor. The validator's errors are checked separately (see <see cref="PlanValidator"/>).
/// </summary>
public static class PlanCompiler
{
    // Team slot palette, in orbit order (2 teams → red/blue, 4 → red/blue/yellow/green).
    private static readonly (string Id, string Name, string Color)[] Palette =
        [("red", "Red", "red"), ("blue", "Blue", "blue"), ("yellow", "Yellow", "yellow"), ("green", "Green", "green")];

    // PGM rejects a nameless destroyable, so the compiler names one rather than the author: "Red Monument",
    // then "Red Monument 2" for a team's second. "Monument" is PGM's own player-facing word for a DTM goal —
    // the codebase reserves the term for the CTW wool monument, but the map's audience is players.
    private static string MonumentName(string teamName, int index) =>
        index == 0 ? $"{teamName} Monument" : $"{teamName} Monument {index + 1}";

    // Distinct dyes for a team's non-first wools, so every wool across the board keys to a unique colour.
    private static readonly string[] Dyes =
        ["orange", "light_blue", "pink", "lime", "cyan", "purple", "magenta", "white", "gray", "brown", "black", "silver"];

    public static (SketchLayout Layout, MapIntent Intent) Compile(PlanModel plan)
    {
        // Declare the plan's negative space before reading it, so a body's enclosed void is a buffer piece
        // whether or not the author drew one (PlanVoids). Idempotent, so compiling twice changes nothing.
        var declared = PlanVoids.Declare(plan);
        var d = ContactGraph.Build(declared);
        var layout = BuildLayout(declared, d);
        var intent = BuildIntent(declared, d, layout);
        AppendStructuralShapes(layout, intent);
        return (layout, intent);
    }

    // Project the plan's placed structural pieces — spawn buildings and wool rooms — into the layout as
    // locked annotation rectangles (S25), so refining a plan in the sketch keeps them visible instead of
    // letting them dissolve into the fused island polygon. The intent's piece rect is the whole story: it is
    // the protection/room region, it sizes the stamped foundation, and the marker sits relative to it, so the
    // rectangle alone re-secures the link back to the intent entity. Role-tagged, so the rasterizer skips them
    // (no terrain contribution — the ground under them is already the island) and the sketch renders them
    // read-only. Only pieces with a role rect are projected: a plain-piece or hand-authored spawn/wool has no
    // Piece and keeps nothing to surface here.
    private static void AppendStructuralShapes(SketchLayout layout, MapIntent intent)
    {
        var shapes = layout.Layout?.Shapes;
        if (shapes is null) return;
        var teamColor = (intent.Teams ?? []).ToDictionary(t => t.Id, t => t.Color);

        foreach (var s in intent.Spawns)
            if (s.Piece is { } rect)
                shapes.Add(StructureShape($"spawn-{s.Team}", "spawn", s.Team, teamColor.GetValueOrDefault(s.Team, s.Team), rect));

        foreach (var w in intent.Wools ?? [])
            if (w.Piece is { } rect)
            {
                var color = !string.IsNullOrEmpty(w.Color) ? w.Color : teamColor.GetValueOrDefault(w.Owner, w.Owner);
                shapes.Add(StructureShape($"wool-{w.Owner}-{color}", "woolRoom", $"{w.Owner}:{color}", color, rect));
            }
    }

    private static SketchShape StructureShape(string id, string role, string intentRef, string color, Rect rect) => new()
    {
        Id = id,
        Type = "rectangle",
        Operation = "add",
        Role = role,
        IntentRef = intentRef,
        Color = color,
        MinX = rect.MinX, MinZ = rect.MinZ, MaxX = rect.MaxX, MaxZ = rect.MaxZ,
    };

    // ── layout: unioned shapes + mirror islands + framing ───────────────────────────────────────────────

    private static SketchLayout BuildLayout(PlanModel plan, ContactGraph d)
    {
        var shapes = new List<SketchShape>();
        var islandShapes = new List<(bool Mirrors, string ShapeId)>();
        var shapeIndex = 0;

        foreach (var component in d.Components)
        {
            var pieces = component.Select(id => d.Piece(id)!.Value).ToList();
            if (pieces.Select(p => p.Mirrors).Distinct().Count() > 1)
                throw new InvalidOperationException($"component [{string.Join(", ", component)}] mixes mirrored and non-mirrored pieces");
            // one shape per distinct surface within the component (a stepped island → stacked plateaus); a
            // surface whose pieces fall into several disjoint patches (connected only through pieces of a
            // different surface) emits one shape per patch, so no patch is dropped.
            foreach (var group in pieces.GroupBy(p => p.Surface).OrderBy(g => g.Key))
            {
                var rects = group.Select(p => (p.Rect.MinX, p.Rect.MinZ, p.Rect.MaxX, p.Rect.MaxZ)).ToList();
                foreach (var ring in RectilinearUnion.Outlines(rects))
                {
                    var id = $"s{shapeIndex++}";
                    shapes.Add(new SketchShape
                    {
                        Id = id,
                        Type = "polygon",
                        Operation = "add",
                        BaseHeight = group.Key,
                        Vertices = [.. ring],
                    });
                    islandShapes.Add((group.First().Mirrors, id));
                }
            }
        }

        // Buffers state the negative space (PlanVoids): each becomes a subtract shape, so the ground a body
        // encircles stays void instead of taking the fill its outline implies. A buffer never takes terrain
        // away from a piece — it is clipped to what no generating piece covers, which is what keeps it the
        // inert annotation it has always been where it overlaps one.
        var terrain = d.Pieces.Select(p => (p.Rect.MinX, p.Rect.MinZ, p.Rect.MaxX, p.Rect.MaxZ)).ToList();
        foreach (var buffer in plan.Pieces.Where(p => p.Role == PlanRoles.Buffer))
        {
            var rect = ContactGraph.ToBlock(buffer.Rect, d.Cell);
            foreach (var ring in RectilinearUnion.Difference([(rect.MinX, rect.MinZ, rect.MaxX, rect.MaxZ)], terrain))
            {
                var id = $"s{shapeIndex++}";
                shapes.Add(new SketchShape
                {
                    Id = id,
                    Type = "polygon",
                    Operation = "subtract",
                    Vertices = [.. ring],
                });
                islandShapes.Add((buffer.MirrorsOrDefault, id));
            }
        }

        // islands = mirror groups: one per distinct mirrors flag (all-true seeds → a single "team" island)
        var islands = new List<SketchIsland>();
        foreach (var mirrors in islandShapes.Select(s => s.Mirrors).Distinct())
        {
            var ids = islandShapes.Where(s => s.Mirrors == mirrors).Select(s => s.ShapeId).ToList();
            islands.Add(new SketchIsland
            {
                Id = mirrors ? "team" : "neutral",
                Name = mirrors ? "Team island" : "Neutral",
                Mirrors = mirrors,
                ShapeIds = ids,
            });
        }

        return new SketchLayout
        {
            Setup = new SketchSetup
            {
                MirrorMode = d.Mode,
                Center = new SketchCenter { Cx = 0, Cz = 0 },
                Bbox = FannedBbox(shapes, d),
            },
            Layout = new SketchShapes { Shapes = shapes, Islands = islands },
        };
    }

    // The framing box: the extent of every terrain shape's vertices fanned across the orbit, expanded one cell
    // all round. Negative space does not frame the board — a buffer out in the void states a reserved gap, and
    // sizing the view to it would frame ground that is not there.
    private static SketchBbox FannedBbox(List<SketchShape> shapes, ContactGraph d)
    {
        double minX = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxZ = double.MinValue;
        foreach (var s in shapes.Where(s => s.Operation != "subtract"))
            foreach (var v in s.Vertices ?? [])
                for (var k = 0; k < d.Order; k++)
                {
                    var (x, z) = d.FanPoint(v[0], v[1], k);
                    minX = Math.Min(minX, x); minZ = Math.Min(minZ, z);
                    maxX = Math.Max(maxX, x); maxZ = Math.Max(maxZ, z);
                }
        if (minX > maxX) (minX, minZ, maxX, maxZ) = (0, 0, 0, 0);
        return new SketchBbox
        {
            MinX = minX - d.Cell, MinZ = minZ - d.Cell,
            MaxX = maxX + d.Cell, MaxZ = maxZ + d.Cell,
        };
    }

    // ── intent: teams/spawns/wools/build fanned from team 0 ─────────────────────────────────────────────

    private static MapIntent BuildIntent(PlanModel plan, ContactGraph d, SketchLayout layout)
    {
        var order = d.Order;
        var teams = Palette.Take(Math.Max(1, order))
            .Select(s => new TeamDef { Id = s.Id, Name = s.Name, Color = s.Color }).ToList();

        var spawns = new List<SpawnIntent>();
        for (var k = 0; k < order; k++)
            foreach (var s in plan.Placements.Spawns)
            {
                var piece = d.Piece(s.Piece);
                if (piece is null) continue;
                var (bx, bz) = Resolve(piece.Value.Rect, s.At, d.Cell);
                var (fx, fz) = FacingDir(s.Facing);
                var (px, pz) = d.FanPoint(bx, bz, k);
                var prot = d.FanRect(piece.Value.Rect, k);
                spawns.Add(new SpawnIntent
                {
                    Team = teams[k].Id,
                    Point = new Pt(px, piece.Value.Surface, pz),
                    // Protect the whole spawn piece the marker sits on, not just the stamped spawn cube.
                    Protection = [new Rect(prot.MinX, prot.MinZ, prot.MaxX, prot.MaxZ)],
                    // A spawn-role piece sizes the stamped room (WX1); a plain piece keeps the default.
                    Piece = piece.Value.Role == PlanRoles.Spawn
                        ? new Rect(prot.MinX, prot.MinZ, prot.MaxX, prot.MaxZ) : null,
                    // Iron on a spawn-role piece rides the spawn — it resolves beside the room (WX8/WX9);
                    // iron elsewhere keeps the legacy StructureIntent path below.
                    Iron = piece.Value.Role == PlanRoles.Spawn
                        ? [.. plan.Placements.Iron.Where(ir => ir.Piece == s.Piece).Select(ir =>
                            {
                                var (ix, iz) = Resolve(piece.Value.Rect, ir.At, d.Cell);
                                var (fanX, fanZ) = d.FanPoint(ix, iz, k);
                                return new Pt(fanX, piece.Value.Surface, fanZ);
                            })]
                        : [],
                    Yaw = FanYaw(d, bx, bz, fx, fz, k),
                });
            }

        // wools: team-outer, placement-inner (matches the intent's grouping); auto colour with a global dye cursor
        var wools = new List<WoolIntent>();
        var dyeCursor = 0;
        for (var k = 0; k < order; k++)
            for (var i = 0; i < plan.Placements.Wools.Count; i++)
            {
                var w = plan.Placements.Wools[i];
                var piece = d.Piece(w.Piece);
                if (piece is null) continue;
                var (bx, bz) = Resolve(piece.Value.Rect, w.At, d.Cell);
                var (px, pz) = d.FanPoint(bx, bz, k);
                var color = !string.IsNullOrEmpty(w.Color) ? w.Color
                    : i == 0 ? teams[k].Color
                    : Dyes[dyeCursor++ % Dyes.Length];
                var room = d.FanRect(piece.Value.Rect, k);
                // A wool-room ROLE piece sizes the stamped cage and carries its entry interfaces (WX1/WX6);
                // a wool on a plain piece keeps the legacy marker-anchored default cage.
                var isRoomPiece = piece.Value.Role == PlanRoles.WoolRoom;
                wools.Add(new WoolIntent
                {
                    Owner = teams[k].Id,
                    Color = color,
                    // The room region is the whole wool-room piece the marker sits on, not just the stamped cage.
                    Room = [new Rect(room.MinX, room.MinZ, room.MaxX, room.MaxZ)],
                    Piece = isRoomPiece ? new Rect(room.MinX, room.MinZ, room.MaxX, room.MaxZ) : null,
                    Entries = isRoomPiece
                        ? [.. WoolEntrySegments(d, w.Piece).Select(seg =>
                            {
                                var fanned = d.FanRect(seg, k);
                                return new Rect(fanned.MinX, fanned.MinZ, fanned.MaxX, fanned.MaxZ);
                            })]
                        : [],
                    Spawn = new Pt(px, piece.Value.Surface, pz),
                });
            }

        // destroyables: team-outer like wools — a destroyable is a goal one team defends, so an orbit image
        // belongs to the team it lands on. No monument mapping: every other team breaks the same structure.
        //
        // Only at two teams (OB14). Outside that the markers compile to nothing: the validator already errors
        // on such a plan, so the compile endpoint never reaches here, and the only caller that compiles an
        // unvalidated plan is the structure preview — which must not draw four shared goals the export would
        // refuse to build. Declining to fan them is not inventing an answer to the open design question; a
        // preview of the forbidden thing would be.
        var destroyables = new List<DestroyableIntent>();
        for (var k = 0; k < order && order == 2; k++)
            for (var i = 0; i < plan.Placements.Destroyables.Count; i++)
            {
                var b = plan.Placements.Destroyables[i];
                var piece = d.Piece(b.Piece);
                if (piece is null) continue;
                var (bx, bz) = Resolve(piece.Value.Rect, b.At, d.Cell);
                var (px, pz) = d.FanPoint(bx, bz, k);
                destroyables.Add(new DestroyableIntent
                {
                    Owner = teams[k].Id,
                    Name = !string.IsNullOrEmpty(b.Name) ? b.Name : MonumentName(teams[k].Name, i),
                    Style = !string.IsNullOrEmpty(b.Style) ? b.Style : DestroyableStyles.Slug(ObjectiveDefaults.Style),
                    Materials = !string.IsNullOrEmpty(b.Materials) ? b.Materials : ObjectiveDefaults.Materials,
                    Anchor = new Pt(px, piece.Value.Surface, pz),
                    Float = b.Float ?? ObjectiveDefaults.DestroyableFloat,
                });
            }

        // cores: the destroyable's fan with the casing's own knobs. Order-2 only, for the same reason (OB14).
        var cores = new List<CoreIntent>();
        for (var k = 0; k < order && order == 2; k++)
            foreach (var c in plan.Placements.Cores)
            {
                var piece = d.Piece(c.Piece);
                if (piece is null) continue;
                var (bx, bz) = Resolve(piece.Value.Rect, c.At, d.Cell);
                var (px, pz) = d.FanPoint(bx, bz, k);
                cores.Add(new CoreIntent
                {
                    Owner = teams[k].Id,
                    Name = c.Name ?? "",                 // empty is correct: PGM names a core itself
                    Anchor = new Pt(px, piece.Value.Surface, pz),
                    Size = c.Size ?? ObjectiveDefaults.CoreSize,
                    Height = c.Height ?? ObjectiveDefaults.CoreHeight,
                    Shell = c.Shell ?? ObjectiveDefaults.CoreShell,
                    OpenTop = c.OpenTop ?? false,
                    Float = c.Float ?? ObjectiveDefaults.CoreFloat,
                    Leak = c.Leak ?? ObjectiveDefaults.CoreLeak,
                });
            }

        var maxHeight = plan.Globals.Surface + plan.Globals.Headroom;
        var build = new BuildIntent
        {
            MaxHeight = maxHeight,
            Areas = FanRects(plan.BuildZones.Select(z => z.Rect), d),
            Holes = FanRects(plan.BuildZones.SelectMany(z => z.Holes), d),
        };

        // Water lanes fan the same way build areas do, and stay out of the build intent on purpose: a lane
        // that reached the buildable region would be open from the first tick, which is the one thing a lane
        // must not be (docs/contracts/water-lanes.md). A lane's holes are dropped rather than carried — the
        // shared fragment fills a flat region and has nowhere to put a cutout, so honouring one would promise
        // something the export cannot deliver.
        var laneRects = FanRects(plan.WaterLanes.Select(z => z.Rect), d);
        var waterLanes = laneRects.Count > 0 ? new WaterLaneIntent { Rects = laneRects } : null;

        var observerY = plan.Globals.ObserverY ?? plan.Globals.Surface + 15;

        var structures = BuildStructures(plan, d);

        return new MapIntent
        {
            Teams = teams,
            MaxPlayers = plan.Globals.MaxPlayers,
            Spawns = spawns,
            Wools = wools,
            Destroyables = destroyables.Count > 0 ? destroyables : null,
            Cores = cores.Count > 0 ? cores : null,
            Observer = new ObserverIntent { Point = new Pt(0, observerY, 0), Yaw = 0 },
            Build = build,
            WaterLanes = waterLanes,
            Meta = new MetaIntent { Name = plan.Meta?.Name ?? "", Authors = [] },
            Structures = structures.IsEmpty ? null : structures,
        };
    }

    // ── structures: bedrock room floors, entrance redstone, iron cubes, approach walls (ST1–ST4) ─────────
    //
    // Directives are computed once on the authored team-0 unit, then fanned to every orbit image in absolute
    // block coordinates (the world-export path stamps them verbatim). Fanning is deduplicated so a
    // self-symmetric directive lands once.
    private static StructureIntent BuildStructures(PlanModel plan, ContactGraph d)
    {
        var s = new StructureIntent();

        // ST1 room floors — one bedrock column footprint per wool-room piece.
        var floorSeen = new HashSet<(int, int, int, int)>();
        foreach (var piece in d.Pieces.Where(p => p.Role == PlanRoles.WoolRoom))
            for (var k = 0; k < d.Order; k++)
            {
                var r = d.FanRect(piece.Rect, k);
                if (floorSeen.Add((r.MinX, r.MinZ, r.MaxX, r.MaxZ)))
                    s.RoomFloors.Add(new Rect(r.MinX, r.MinZ, r.MaxX, r.MaxZ));
            }

        // ST1 entrance redstone — the last block row inside the room along each entry interface: every
        // terrain↔wool-room land seam, and every build-zone frontline edge on a room piece (WX6 — bridging
        // in through the build region is an entrance like any seam).
        var lineSeen = new HashSet<(int, int, int, int)>();
        var entranceSegments = new List<(BlockRect Room, int X1, int Z1, int X2, int Z2)>();
        foreach (var seg in d.InterfaceSegments.Where(g => g is { WoolRoom: true, Kind: ContactKind.Land }))
        {
            var a = d.Piece(seg.A)!.Value;
            var b = d.Piece(seg.B)!.Value;
            var room = a.Role == PlanRoles.WoolRoom ? a : b;
            entranceSegments.Add((room.Rect, seg.X1, seg.Z1, seg.X2, seg.Z2));
        }
        foreach (var edge in d.FrontlineEdges)
        {
            var piece = d.Piece(edge.Piece);
            if (piece?.Role == PlanRoles.WoolRoom)
                entranceSegments.Add((piece.Value.Rect, edge.X1, edge.Z1, edge.X2, edge.Z2));
        }
        foreach (var (roomRect, sx1, sz1, sx2, sz2) in entranceSegments)
        {
            var (ex1, ez1, ex2, ez2) = EntranceRow(roomRect, sx1, sz1, sx2, sz2);
            // Fan the row as a block-region rect, not as its two endpoints: a block at index c occupies
            // [c, c+1), so it mirrors to block −c−1, not −c. Reflecting the endpoint points lands a mirror
            // image one block off — inset into the room, or past its edge into the void. FanRect re-bounds
            // the fanned corners, carrying the block interval across; the inclusive ends come back as
            // (min, max−1) per axis.
            var row = new BlockRect(
                (int)Math.Min(ex1, ex2), (int)Math.Min(ez1, ez2),
                (int)Math.Max(ex1, ex2) + 1, (int)Math.Max(ez1, ez2) + 1);
            for (var k = 0; k < d.Order; k++)
            {
                var r = d.FanRect(row, k);
                int x1 = r.MinX, z1 = r.MinZ, x2 = r.MaxX - 1, z2 = r.MaxZ - 1;
                if (lineSeen.Add((x1, z1, x2, z2)) && lineSeen.Add((x2, z2, x1, z1)))
                    s.RedstoneLines.Add(new RedstoneLine(x1, z1, x2, z2));
            }
        }

        // ST2/ST3 iron cubes — one per iron marker; renew when the marker's piece is a spawn. Iron on a
        // spawn-role piece that carries a spawn marker is absent here on purpose: it rides that
        // SpawnIntent's Iron list and resolves beside the room (WX8).
        var framedSpawnPieces = plan.Placements.Spawns
            .Select(s => s.Piece).Where(id => d.Piece(id)?.Role == PlanRoles.Spawn).ToHashSet();
        var ironSeen = new HashSet<(int, int)>();
        foreach (var ir in plan.Placements.Iron)
        {
            if (framedSpawnPieces.Contains(ir.Piece)) continue;
            var piece = d.Piece(ir.Piece);
            if (piece is null) continue;
            var (bx, bz) = Resolve(piece.Value.Rect, ir.At, d.Cell);
            var renew = piece.Value.Role == PlanRoles.Spawn;
            for (var k = 0; k < d.Order; k++)
            {
                var (px, pz) = d.FanPoint(bx, bz, k);
                int ax = (int)Math.Round(px, MidpointRounding.AwayFromZero);
                int az = (int)Math.Round(pz, MidpointRounding.AwayFromZero);
                if (ironSeen.Add((ax, az))) s.IronCubes.Add(new IronCube(ax, az, renew));
            }
        }

        // ST4 approach walls — a bedrock barrier over each marked wool-lane interface, on the attack side.
        var wallSeen = new HashSet<(int, int, int, int)>();
        var dist = WoolWalkDistances(plan, d);
        foreach (var c in d.WallInterfaces)
        {
            var (approach, _) = ApproachSide(d, dist, c);
            var pieceA = d.Piece(c.A)!.Value;
            var pieceB = d.Piece(c.B)!.Value;
            var (minX, minZ, maxX, maxZ) = WallFootprint(pieceA, pieceB);
            var topY = approach.Surface + BedrockCourses - 1;
            // The chest face is authored as a piece id, so it survives the orbit: a reflection swaps which of
            // the wall's two faces has the smaller coordinate, and only the piece it looks out at is invariant.
            var chestRect = (d.WallChestPiece(c) == c.B ? pieceB : pieceA).Rect;
            for (var k = 0; k < d.Order; k++)
            {
                var r = d.FanRect(new BlockRect(minX, minZ, maxX, maxZ), k);
                if (!wallSeen.Add((r.MinX, r.MinZ, r.MaxX, r.MaxZ))) continue;
                var face = d.FanRect(chestRect, k);
                var thinAlongX = r.MaxX - r.MinX <= r.MaxZ - r.MinZ;
                var onMinFace = thinAlongX ? face.MaxX <= r.MinX + 1 : face.MaxZ <= r.MinZ + 1;
                s.Walls.Add(new WallStructure(r.MinX, r.MinZ, r.MaxX, r.MaxZ, topY, onMinFace));
            }
        }

        return s;
    }

    /// <summary>Courses of bedrock an approach wall stands above the ground it bars (ST4). Three, plus the
    /// cobweb course the stamper caps it with — tall enough to stop a player walking or jumping the line,
    /// short enough that both halves of the lane still read as one place.</summary>
    private const int BedrockCourses = 3;

    // The redstone row: one block inside the wool room, running the entry-interface width, with the two
    // endpoints (where the torches sit). The segment lies on the room piece's boundary — a piece↔piece
    // border or a build-zone frontline edge — and the room's side of it picks the row.
    private static (double X1, double Z1, double X2, double Z2) EntranceRow(
        BlockRect room, int x1, int z1, int x2, int z2)
    {
        if (x1 == x2)   // vertical seam at x = seam; room lies to one side
        {
            int seamX = x1;
            int col = room.MinX == seamX ? seamX : seamX - 1;   // first room block column
            int loZ = Math.Min(z1, z2), hiZ = Math.Max(z1, z2);
            return (col, loZ, col, hiZ - 1);
        }
        else            // horizontal seam at z = seam
        {
            int seamZ = z1;
            int row = room.MinZ == seamZ ? seamZ : seamZ - 1;
            int loX = Math.Min(x1, x2), hiX = Math.Max(x1, x2);
            return (loX, row, hiX - 1, row);
        }
    }

    /// <summary>The entry interfaces of a wool-room piece (WX6), as degenerate block rects on its boundary
    /// (zero thickness across the seam): every terrain↔room land seam plus every build-zone frontline edge.
    /// The world exporter cuts the cage doors and lays the entrance redstone on exactly these; empty means
    /// the room is unreachable, which the validator refuses.</summary>
    internal static List<BlockRect> WoolEntrySegments(ContactGraph d, string pieceId)
    {
        var segments = new List<BlockRect>();
        foreach (var seg in d.InterfaceSegments)
            if (seg is { WoolRoom: true, Kind: ContactKind.Land } && (seg.A == pieceId || seg.B == pieceId))
                segments.Add(new BlockRect(Math.Min(seg.X1, seg.X2), Math.Min(seg.Z1, seg.Z2),
                    Math.Max(seg.X1, seg.X2), Math.Max(seg.Z1, seg.Z2)));
        foreach (var edge in d.FrontlineEdges)
            if (edge.Piece == pieceId)
                segments.Add(new BlockRect(Math.Min(edge.X1, edge.X2), Math.Min(edge.Z1, edge.Z2),
                    Math.Max(edge.X1, edge.X2), Math.Max(edge.Z1, edge.Z2)));
        return segments;
    }

    // A wall footprint: two blocks thick across the shared seam, the full interface width along it.
    private static (int MinX, int MinZ, int MaxX, int MaxZ) WallFootprint(DerivedPiece a, DerivedPiece b)
    {
        var (x1, z1, x2, z2) = ContactGraph.BorderSegment(a.Rect, b.Rect);
        if (x1 == x2)   // vertical seam
            return (x1 - 1, Math.Min(z1, z2), x1 + 1, Math.Max(z1, z2));
        return (Math.Min(x1, x2), z1 - 1, Math.Max(x1, x2), z1 + 1);   // horizontal seam
    }

    // The attack side of a wall pair: the piece with the larger walk-graph distance to the nearest same-unit
    // wool marker (attackers approach from farther out); a tie breaks to the lower-surface side.
    private static (DerivedPiece Approach, DerivedPiece Defence) ApproachSide(
        ContactGraph d, IReadOnlyDictionary<string, int> dist, Contact c)
    {
        var a = d.Piece(c.A)!.Value;
        var b = d.Piece(c.B)!.Value;
        int da = dist.GetValueOrDefault(a.Id, int.MaxValue);
        int db = dist.GetValueOrDefault(b.Id, int.MaxValue);
        if (da != db) return da > db ? (a, b) : (b, a);
        return a.Surface <= b.Surface ? (a, b) : (b, a);   // tie → lower surface is the approach
    }

    // Hop distance from each piece to the nearest wool-marker piece over the walk graph (land interfaces +
    // gap links). Unreached pieces are absent (treated as infinite by callers).
    private static Dictionary<string, int> WoolWalkDistances(PlanModel plan, ContactGraph d)
    {
        var adj = d.Pieces.ToDictionary(p => p.Id, _ => new List<string>());
        void Link(string x, string y) { if (adj.ContainsKey(x) && adj.ContainsKey(y)) { adj[x].Add(y); adj[y].Add(x); } }
        foreach (var c in d.LandInterfaces) Link(c.A, c.B);
        foreach (var g in d.GapLinks) Link(g.A, g.B);

        var dist = new Dictionary<string, int>();
        var q = new Queue<string>();
        foreach (var wp in plan.Placements.Wools.Select(w => w.Piece).Distinct())
            if (adj.ContainsKey(wp) && dist.TryAdd(wp, 0)) q.Enqueue(wp);
        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            foreach (var nb in adj[cur])
                if (dist.TryAdd(nb, dist[cur] + 1)) q.Enqueue(nb);
        }
        return dist;
    }

    // Fan a set of cell rects to every orbit image, de-duplicating exact repeats (self-symmetric rects).
    private static List<Rect> FanRects(IEnumerable<CellRect> cellRects, ContactGraph d)
    {
        var areas = new List<Rect>();
        var seen = new HashSet<(int, int, int, int)>();
        foreach (var cr in cellRects)
        {
            var block = ContactGraph.ToBlock(cr, d.Cell);
            for (var k = 0; k < d.Order; k++)
            {
                var r = d.FanRect(block, k);
                if (seen.Add((r.MinX, r.MinZ, r.MaxX, r.MaxZ)))
                    areas.Add(new Rect(r.MinX, r.MinZ, r.MaxX, r.MaxZ));
            }
        }
        return areas;
    }

    // Piece-relative half-cell offset → block coordinate (piece origin + offset·cell). A .5 offset lands on a
    // 2.5-block half-cell; downstream flooring/snapping is the export pipeline's job, so the raw value flows on.
    private static (double X, double Z) Resolve(BlockRect piece, double[] at, int cell) =>
        (piece.MinX + at[0] * cell, piece.MinZ + at[1] * cell);

    // The facing unit direction on the authored unit: absolute board directions (front = −z, back = +z,
    // left = −x, right = +x), the fixed reading the editor renders. Each orbit image fans this vector.
    private static (int Dx, int Dz) FacingDir(string facing) => facing switch
    {
        "back"  => (0, 1),
        "left"  => (-1, 0),
        "right" => (1, 0),
        _ => (0, -1),                                                     // "front"
    };

    // The k-th orbit image's yaw: fan the facing as a direction (image of point+dir minus image of point).
    private static double FanYaw(ContactGraph d, double x, double z, int dx, int dz, int k)
    {
        var p = d.FanPoint(x, z, k);
        var q = d.FanPoint(x + dx, z + dz, k);
        var yaw = Math.Round(Math.Atan2(-(q.X - p.X), q.Z - p.Z) * 180 / Math.PI);
        return ((yaw % 360) + 360) % 360;
    }
}
