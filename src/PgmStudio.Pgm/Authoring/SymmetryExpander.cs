namespace PgmStudio.Pgm.Authoring;

using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Vocabulary;

/// <summary>
/// Orbit-fill for the declarative intent (docs/pgm/new-map-authoring.md §3). Given an intent whose
/// <see cref="MapIntent.Symmetry"/> is set, the author only authors <b>one orbit unit</b> — team 0's
/// spawn, one wool — and this expander rotates/reflects it onto the other teams so the rest of the map
/// configuration falls out for free. The expansion happens at the <i>intent</i> level (before any
/// generator runs): it returns a new <see cref="MapIntent"/> whose <c>Spawns</c>/<c>Wools</c> cover every
/// team, which the slice generators then project unchanged.
/// <para><b>Orbit ordering.</b> <see cref="MapIntent.Teams"/> are taken to be listed <i>in orbit order</i>:
/// step <c>k</c> of the symmetry maps the unit owned by <c>Teams[i]</c> onto <c>Teams[(i+k) % n]</c>.
/// For a clean symmetric map the team count equals the symmetry order (<c>rot_90</c>→4, mirrors/<c>rot_180</c>→2)
/// so each team is filled exactly once.</para>
/// <para><b>A filled image states no id.</b> It is not a piece the intent stores, so it cannot carry the id of
/// the one it was turned from; <see cref="PlayIds.Assign"/> names it where a reader needs one.</para>
/// <para><b>Author overrides win.</b> A team that already has an authored spawn/wool is never overwritten —
/// only missing teams are filled. So the author can hand-correct any orbit member after the fill.</para>
/// <para>Geometry uses the canonical <see cref="Symmetry"/> transforms. Build areas <i>are</i> orbited too
/// (the author draws the bridges on one side; <see cref="OrbitBuild"/> mirrors each onto the other sides and
/// dedups any centre/axis piece) — but, unlike spawns/wools, they carry no per-team identity, so they orbit
/// as a flat set of rects rather than a team-indexed fill.</para>
/// </summary>
public static class SymmetryExpander
{
    /// <summary>Return an intent with teams (synthesized if absent), spawns/wools orbit-filled across all
    /// teams, and build areas orbited. No-op (returns the input) when there's no symmetry or an unknown
    /// mode.</summary>
    public static MapIntent Expand(MapIntent intent)
    {
        if (intent.Symmetry is not { } sym || string.IsNullOrWhiteSpace(sym.Mode)) return intent;
        if (!IsKnown(sym.Mode)) return intent;

        var order = Symmetry.Order(sym.Mode);
        // Teams the author listed win; otherwise derive count from the symmetry and synthesize them (§4),
        // so "symmetry + one spawn" is a complete map. Orbit positions map onto this team list in order.
        var teams = TeamsOf(intent, order);
        if (teams.Count <= 1) return intent;

        // `with`, never a fresh MapIntent: a rebuild that names its fields drops every slice added after it
        // was written, which is exactly what happened — destroyables, cores, island teams and the plan's
        // stamped structures were all deleted here, silently, on any intent carrying a symmetry.
        return intent with
        {
            Teams = teams,
            Spawns = FillSpawns(intent.Spawns, teams, sym, order),
            Build = OrbitBuild(intent.Build, sym, order),
            WaterLanes = OrbitWaterLanes(intent.WaterLanes, sym, order),
            Wools = FillWools(intent.Wools, teams, sym, order),
            Destroyables = FillDestroyables(intent.Destroyables, teams, sym, order),
            Cores = FillCores(intent.Cores, teams, sym, order),
            ControlPoints = FillControlPoints(intent.ControlPoints, sym, order),
            Flags = FillFlags(intent.Flags, teams, sym, order),
            ScoreBoxes = FillScoreBoxes(intent.ScoreBoxes, teams, sym, order),
            Zones = FillZones(intent.Zones, teams, sym, order),
        };
    }

    // ── team synthesis ──────────────────────────────────────────────────────────────────
    // No teams listed → derive count from the symmetry order and assign palette colours. Team 0's id is
    // anchored to whatever the author authored against (their one spawn/wool) so the orbit attaches to it.
    /// <summary>The teams a map with <paramref name="order"/> symmetry images plays, in orbit order: the ones the
    /// intent lists, or one per image named from the team palette.</summary>
    public static List<TeamDef> TeamsOf(MapIntent intent, int order) =>
        intent.Teams is { Count: > 0 } provided ? provided : SynthesizeTeams(intent, order);

    private static List<TeamDef> SynthesizeTeams(MapIntent intent, int order)
    {
        var colors = TeamPalette.Take(order);
        var anchorId = intent.Spawns.FirstOrDefault()?.Team
                       ?? intent.Wools?.FirstOrDefault()?.Owner;
        var teams = new List<TeamDef>();
        for (var i = 0; i < colors.Count; i++)
        {
            var color = colors[i];
            // Non-anchor palette teams take the corpus/template `<colour>-team` id (the anchor keeps the
            // intent's id, which the UI already suffixes); IntentNaming.Slug strips `-team` for derived ids.
            var id = i == 0 && !string.IsNullOrWhiteSpace(anchorId) ? anchorId! : color.Replace(' ', '-') + "-team";
            teams.Add(new TeamDef { Id = id, Name = TeamPalette.Label(color), Color = color });
        }
        return teams;
    }

    // ── build areas + holes ─────────────────────────────────────────────────────────────────
    // Orbit every authored build rect (and every no-build hole) onto the other sides, dedup by (rounded)
    // bounds — a centre/axis piece maps onto itself and collapses, an off-axis footprint/bridge/hole fills
    // its partner(s). Overlaps between distinct rects are left to the union (PGM tolerates them); build
    // areas carry no team. Holes orbit so a symmetric map keeps symmetric cutouts.
    private static BuildIntent? OrbitBuild(BuildIntent? build, SymmetryIntent sym, int order)
    {
        if (build is not { } b) return build;
        if (b.Areas.Count == 0 && b.Holes.Count == 0) return build;
        // `with`, not a named rebuild — see the MapIntent-level comment above: a field this method does not
        // name keeps its value instead of being dropped on every symmetric map.
        return b with { Areas = OrbitAreas(b.Areas, sym, order), Holes = OrbitRects(b.Holes, sym, order) };
    }

    // Lanes orbit exactly as build areas do — same rects, same dedup — so a lane drawn on one side opens on
    // every side at once. A lane straddling the axis maps onto itself and collapses to one.
    private static WaterLaneIntent? OrbitWaterLanes(WaterLaneIntent? lanes, SymmetryIntent sym, int order)
    {
        if (lanes is not { Rects.Count: > 0 } l) return lanes;
        return new WaterLaneIntent { Rects = OrbitAreas(l.Rects, sym, order) };
    }

    // An area keeps its id and each image it adds states none, the way a filled spawn does.
    private static List<PieceArea> OrbitAreas(List<PieceArea> areas, SymmetryIntent sym, int order)
    {
        var seen = new HashSet<(double, double, double, double)>();
        var outp = new List<PieceArea>();
        void Add(PieceArea area) { if (seen.Add((area.MinX, area.MinZ, area.MaxX, area.MaxZ))) outp.Add(area); }
        foreach (var area in areas)
        {
            Add(area);
            for (var k = 1; k < order; k++) Add(TransformRect(area.Rect, sym, k));
        }
        return outp;
    }

    private static List<Rect> OrbitRects(List<Rect> rects, SymmetryIntent sym, int order)
    {
        var seen = new HashSet<(double, double, double, double)>();
        var outp = new List<Rect>();
        void Add(Rect r) { if (seen.Add((r.MinX, r.MinZ, r.MaxX, r.MaxZ))) outp.Add(r); }
        foreach (var r in rects)
        {
            Add(r);
            for (var k = 1; k < order; k++) Add(TransformRect(r, sym, k));
        }
        return outp;
    }

    // ── spawns ──────────────────────────────────────────────────────────────────────────

    /// <summary>The identity an authored entry carries into its orbit. An intent compiled from a plan already
    /// has one; one authored in Configure does not, so it is given its list index here — <b>before</b> the
    /// fan, because after it the images are exactly what cannot be told apart. Every image then differs from
    /// the authored unit only in <see cref="StampId.Image"/>.</summary>
    private static StampId Seed(StampId stamp, string kind, int index)
        => !string.IsNullOrEmpty(stamp.Kind) ? stamp
         : new StampId(kind, index.ToString(System.Globalization.CultureInfo.InvariantCulture), 0);

    private static List<SpawnIntent> FillSpawns(List<SpawnIntent> authored, List<TeamDef> teams, SymmetryIntent sym, int order)
    {
        var seeded = authored.Select((s, index) => s with { Stamp = Seed(s.Stamp, "spawn", index) }).ToList();
        var result = new List<SpawnIntent>(seeded);
        var have = new HashSet<string>(seeded.Select(s => s.Team));
        foreach (var src in seeded)
        {
            var i = IndexOfTeam(teams, src.Team);
            if (i < 0) continue;
            for (var k = 1; k < order; k++)
            {
                var target = teams[(i + k) % teams.Count].Id;
                if (!have.Add(target)) continue;   // authored or already filled — keep the existing one
                result.Add(src with
                {
                    Team = target,
                    Id = "", Stamp = src.Stamp.At(k),
                    Reflected = src.Reflected != Symmetry.Reflects(sym.Mode, k),
                    Point = TransformPt(src.Point, sym, k),
                    Protection = src.Protection.Select(r => TransformRect(r, sym, k)).ToList(),
                    Yaw = TransformYaw(src.Yaw, sym, k),
                    House = src.House?.Mapped(rect => TransformRect(rect, sym, k)),
                    Iron = [.. src.Iron.Select(iron => TransformPt(iron, sym, k))],
                    Doors = TransformDoors(src.Doors, sym, k),
                });
            }
        }
        return result;
    }

    // ── wools ───────────────────────────────────────────────────────────────────────────
    private static List<WoolIntent>? FillWools(List<WoolIntent>? authored, List<TeamDef> teams, SymmetryIntent sym, int order)
    {
        if (authored is null) return null;
        var seeded = authored.Select((w, index) => w with { Stamp = Seed(w.Stamp, "wool", index) }).ToList();
        var result = new List<WoolIntent>(seeded);
        var have = new HashSet<string>(seeded.Select(w => w.Owner));
        foreach (var src in seeded)
        {
            var i = IndexOfTeam(teams, src.Owner);
            if (i < 0) continue;
            for (var k = 1; k < order; k++)
            {
                var owner = teams[(i + k) % teams.Count];
                if (!have.Add(owner.Id)) continue;
                result.Add(src with
                {
                    Owner = owner.Id,
                    Id = "", Stamp = src.Stamp.At(k),
                    Reflected = src.Reflected != Symmetry.Reflects(sym.Mode, k),
                    Color = "",   // orbit copies default to the new owner team's colour (WoolGenerator.ColorSlug)
                    Protection = src.Protection.Select(r => TransformRect(r, sym, k)).ToList(),
                    House = src.House?.Mapped(rect => TransformRect(rect, sym, k)),
                    Doors = TransformDoors(src.Doors, sym, k),
                    Spawn = TransformBlock(src.Spawn, sym, k),
                    Monuments = [.. src.Monuments.Select(m => m with
                    {
                        // shift the capturing team by the same orbit step; transform its capture block
                        Team = RemapTeam(teams, m.Team, k),
                        Location = TransformBlock(m.Location, sym, k),
                    })],
                });
            }
        }
        return result;
    }

    // ── destroyables + cores ─────────────────────────────────────────────────────────────
    // The wool fill without the parts a wool has and these do not: no colour, and no per-capturing-team
    // monuments, because every other team breaks the same structure. What does carry is the whole shape of
    // it — style, material, casing size and the float/leak pair — so a goal authored once is the same goal
    // for every team. A map whose two cores differ is not a symmetric map.
    //
    // The resolved box orbits with the anchor. It is the region the goal is scoped by (OB8), and on a
    // symmetric world the mirrored structure stands in the mirrored volume; leaving it unmapped would scope
    // one team's goal to the other team's blocks.

    private static List<DestroyableIntent>? FillDestroyables(
        List<DestroyableIntent>? authored, List<TeamDef> teams, SymmetryIntent sym, int order)
    {
        if (authored is null) return null;
        var seeded = authored.Select((d, index) => d with { Stamp = Seed(d.Stamp, "destroyable", index) }).ToList();
        var result = new List<DestroyableIntent>(seeded);
        var have = new HashSet<string>(seeded.Select(d => d.Owner));
        foreach (var src in seeded)
        {
            var index = IndexOfTeam(teams, src.Owner);
            if (index < 0) continue;
            for (var k = 1; k < order; k++)
            {
                var owner = teams[(index + k) % teams.Count];
                if (!have.Add(owner.Id)) continue;
                result.Add(src with
                {
                    Owner = owner.Id,
                    Id = "", Stamp = src.Stamp.At(k),
                    // PGM rejects a nameless destroyable, and one name for both teams reads as one goal —
                    // so the copy takes its new owner's, the same form the plan compiler mints.
                    Name = $"{owner.Name} Monument",
                    Anchor = TransformAnchor(src.Anchor, sym, k, DestroyableFootprint(src.Style)),
                    Box = TransformBox(src.Box, sym, k),
                });
            }
        }
        return result;
    }

    private static List<CoreIntent>? FillCores(
        List<CoreIntent>? authored, List<TeamDef> teams, SymmetryIntent sym, int order)
    {
        if (authored is null) return null;
        var seeded = authored.Select((c, index) => c with { Stamp = Seed(c.Stamp, "core", index) }).ToList();
        var result = new List<CoreIntent>(seeded);
        var have = new HashSet<string>(seeded.Select(core => core.Owner));
        foreach (var src in seeded)
        {
            var index = IndexOfTeam(teams, src.Owner);
            if (index < 0) continue;
            for (var k = 1; k < order; k++)
            {
                var owner = teams[(index + k) % teams.Count];
                if (!have.Add(owner.Id)) continue;
                // The name rides through as authored: PGM names a core per team itself, so an empty one is
                // correct and a chosen one is the author's word for the pair.
                result.Add(src with
                {
                    Owner = owner.Id,
                    Id = "", Stamp = src.Stamp.At(k),
                    Anchor = TransformAnchor(src.Anchor, sym, k, ObjectiveFootprint.Core(src.Size)),
                    Box = TransformBox(src.Box, sym, k),
                });
            }
        }
        return result;
    }

    /// <summary>Score boxes stated for one team, fanned onto every other team's image of the box as a core is:
    /// a team already holding a box keeps its own.</summary>
    public static List<ScoreBoxIntent>? FillScoreBoxes(List<ScoreBoxIntent>? authored, List<TeamDef> teams, SymmetryIntent sym) =>
        IsKnown(sym.Mode) ? FillScoreBoxes(authored, teams, sym, Symmetry.Order(sym.Mode)) : authored;

    private static List<ScoreBoxIntent>? FillScoreBoxes(
        List<ScoreBoxIntent>? authored, List<TeamDef> teams, SymmetryIntent sym, int order)
    {
        if (authored is null) return null;
        var result = new List<ScoreBoxIntent>(authored);
        var have = new HashSet<string>(authored.Select(box => box.Team));
        foreach (var src in authored)
        {
            var index = IndexOfTeam(teams, src.Team);
            if (index < 0) continue;
            for (var k = 1; k < order; k++)
            {
                var team = teams[(index + k) % teams.Count];
                if (!have.Add(team.Id)) continue;
                result.Add(src with { Team = team.Id, Box = TransformBox(src.Box, sym, k)!.Value });
            }
        }
        return result;
    }

    /// <summary>Zones fanned across the orbit: a team's zone onto every other team's image of it, a team already
    /// holding one keeping its own, and a zone with no team onto its images by position, as a build region is.</summary>
    private static List<ZoneIntent>? FillZones(List<ZoneIntent>? authored, List<TeamDef> teams, SymmetryIntent sym, int order)
    {
        if (authored is null) return null;
        var result = new List<ZoneIntent>();
        var owned = authored.Where(zone => zone.Team.Length > 0).ToList();
        var have = new HashSet<(string Id, string Team)>(owned.Select(zone => (zone.Id, zone.Team)));
        foreach (var zone in authored)
        {
            if (zone.Team.Length == 0) { result.Add(zone with { Area = OrbitRects(zone.Area, sym, order) }); continue; }
            result.Add(zone);
            var index = IndexOfTeam(teams, zone.Team);
            if (index < 0) continue;
            for (var k = 1; k < order; k++)
            {
                var team = teams[(index + k) % teams.Count];
                if (!have.Add((zone.Id, team.Id))) continue;
                result.Add(zone with { Team = team.Id, Area = [.. zone.Area.Select(rect => TransformRect(rect, sym, k))] });
            }
        }
        return result;
    }

    /// <summary>
    /// Fan the capture points across the orbit. <b>The one fan here that is not keyed on a team</b>: a point
    /// belongs to nobody, so what is orbited is its position rather than its owner, and the orbit's order
    /// rather than the team count decides how many images it has.
    ///
    /// <para>A point standing on the centre of symmetry is <b>its own image</b> and stays one point. That is
    /// not a special case bolted on — it falls out of comparing each image to the points already placed — and
    /// it is what makes the author's rule authorable: state the middle and one side, get a middle and a
    /// matched pair on two teams, a middle and a ring of four on four.</para>
    ///
    /// <para>Images of a <em>named</em> point take the name with an index, because two rows reading "Side" in
    /// the sidebar name one hill twice. An unnamed point stays unnamed on every image: PGM numbers those
    /// itself, better than anything invented here.</para>
    /// </summary>
    /// <summary>Capture points stated once each, fanned across <paramref name="sym"/>: every image added, named
    /// for its point and numbered on, and one already standing where an image would go taken as that image.</summary>
    public static List<ControlPointIntent>? FillControlPoints(List<ControlPointIntent>? authored, SymmetryIntent sym) =>
        IsKnown(sym.Mode) ? FillControlPoints(authored, sym, Symmetry.Order(sym.Mode)) : authored;

    /// <summary>Generators stated once each, fanned across <paramref name="sym"/> as capture points are: every
    /// image added under its generator's id numbered on — the regions minted from it stay one per generator —
    /// and one already standing where an image would go taken as that image.</summary>
    public static List<SpawnerIntent>? FillSpawners(List<SpawnerIntent>? authored, SymmetryIntent sym)
    {
        if (authored is null || !IsKnown(sym.Mode)) return authored;
        var order = Symmetry.Order(sym.Mode);
        var result = new List<SpawnerIntent>(authored);
        var placed = authored.Select(spawner => (spawner.At.X, spawner.At.Z)).ToList();
        foreach (var src in authored)
            for (var k = 1; k < order; k++)
            {
                var at = TransformPt(src.At, sym, k);
                if (placed.Any(seen => SamePoint(seen, (at.X, at.Z)))) continue;
                placed.Add((at.X, at.Z));
                result.Add(src with { Id = $"{src.Id}-{k + 1}", At = at });
            }
        return result;
    }

    private static List<ControlPointIntent>? FillControlPoints(
        List<ControlPointIntent>? authored, SymmetryIntent sym, int order)
    {
        if (authored is null) return null;
        var result = authored.Select((p, index) => p with { Stamp = Seed(p.Stamp, "controlpoint", index) }).ToList();
        var placed = result.Select(p => Pad(p.Anchor, p.Size)).ToList();

        foreach (var src in result.ToList())
            for (var k = 1; k < order; k++)
            {
                var anchor = TransformAnchor(src.Anchor, sym, k, ObjectiveFootprint.ControlPoint(PadSize(src.Size)));
                // An image overlapping a pad already placed is that pad: its own image, which about a centre on a
                // grid line is the middle pad one block over, or a point another authored one already holds.
                var pad = Pad(anchor, src.Size);
                if (placed.Any(seen => Overlap(seen, pad))) continue;
                placed.Add(pad);
                result.Add(src with
                {
                    Id = src.Id.Length > 0 ? $"{src.Id}-{k + 1}" : "",
                    Stamp = src.Stamp.At(k),
                    Name = src.Name.Length > 0 ? $"{src.Name} {k + 1}" : "",
                    Anchor = anchor,
                    PadBox = TransformBox(src.PadBox, sym, k),
                    CaptureBox = TransformBox(src.CaptureBox, sym, k),
                });
            }
        return result;
    }

    /// <summary>Flags fanned across <paramref name="sym"/>. One nobody owns keeps its place in the list and gains
    /// its posts' images as capture points do: every image of a post added after the authored ones and named on,
    /// its facing turned with it, and one already standing where an image would go taken as that image. A team's
    /// flag fans by team as a core does: each other team gains its image — posts and net turned, named and
    /// coloured for that team — and a team already owning a flag keeps its own.</summary>
    public static List<FlagIntent>? FillFlags(List<FlagIntent>? authored, List<TeamDef> teams, SymmetryIntent sym) =>
        IsKnown(sym.Mode) ? FillFlags(authored, teams, sym, Symmetry.Order(sym.Mode)) : authored;

    private static List<FlagIntent>? FillFlags(List<FlagIntent>? authored, List<TeamDef> teams, SymmetryIntent sym, int order)
    {
        if (authored is null) return null;
        var stamped = authored.Select((flag, f) => flag with
        {
            Posts = [.. flag.Posts.Select((post, i) => post with
            {
                Stamp = !string.IsNullOrEmpty(post.Stamp.Kind) ? post.Stamp : new StampId("flagpost", $"{f}-{i}", 0),
            })],
        }).ToList();

        var result = stamped.Select(flag => flag.Owned ? flag : flag with { Posts = FillPosts(flag.Posts) }).ToList();
        var have = new HashSet<string>(stamped.Where(flag => flag.Owned).Select(flag => IntentNaming.TeamId(flag.Team)));
        foreach (var src in stamped.Where(flag => flag.Owned))
        {
            var index = IndexOfTeam(teams, src.Team);
            if (index < 0) continue;
            for (var k = 1; k < order; k++)
            {
                var team = teams[(index + k) % teams.Count];
                if (!have.Add(IntentNaming.TeamId(team.Id))) continue;
                result.Add(src with
                {
                    Team = team.Id,
                    Name = "",
                    Color = null,
                    Net = TransformBox(src.Net, sym, k),
                    Posts = [.. src.Posts.Select(post => Turned(post, k) with { Name = post.Name })],
                });
            }
        }
        return result;

        FlagPostIntent Turned(FlagPostIntent post, int k) => post with
        {
            Stamp = post.Stamp.At(k),
            At = TransformBlock(post.At, sym, k),
            Yaw = post.Yaw is { } yaw ? TransformYaw(yaw, sym, k) : null,
            BannerBox = TransformBox(post.BannerBox, sym, k),
        };

        // A post's image on the block beside a placed post, or on it, is that post: about a centre on a grid
        // line no block is its own image, so the middle post's image is its neighbour.
        List<FlagPostIntent> FillPosts(List<FlagPostIntent> posts)
        {
            var filled = new List<FlagPostIntent>(posts);
            var placed = filled.Select(post => Cell(post.At)).ToList();
            foreach (var src in posts)
                for (var k = 1; k < order; k++)
                {
                    var image = Turned(src, k);
                    var cell = Cell(image.At);
                    if (placed.Any(seen => Math.Abs(seen.X - cell.X) <= 1 && Math.Abs(seen.Z - cell.Z) <= 1)) continue;
                    placed.Add(cell);
                    filled.Add(image with { Name = src.Name.Length > 0 ? $"{src.Name} {k + 1}" : "" });
                }
            return filled;
        }
    }

    // Half a block: a position is authored on the half-block lattice, so two nearer than that are one.
    private static bool SamePoint((double X, double Z) a, (double X, double Z) b)
        => Math.Abs(a.X - b.X) < 0.5 && Math.Abs(a.Z - b.Z) < 0.5;

    private static (int X, int Z) Cell(Pt at) => ((int)Math.Floor(at.X), (int)Math.Floor(at.Z));

    private static int PadSize(int size)
        => Math.Clamp(size, ObjectiveDefaults.MinControlPointSize, ObjectiveDefaults.MaxControlPointSize);

    /// <summary>The cells a capture point's pad covers, as the stamper lays it.</summary>
    private static (int MinX, int MinZ, int MaxX, int MaxZ) Pad(Pt anchor, int size)
    {
        var (x, z) = ObjectiveFootprint.AnchorCell(anchor.X, anchor.Z);
        var (width, depth) = ObjectiveFootprint.ControlPoint(PadSize(size));
        return ObjectiveFootprint.Centred(x, z, width, depth);
    }

    private static bool Overlap((int MinX, int MinZ, int MaxX, int MaxZ) one, (int MinX, int MinZ, int MaxX, int MaxZ) other)
        => one.MinX <= other.MaxX && other.MinX <= one.MaxX && one.MinZ <= other.MaxZ && other.MinZ <= one.MaxZ;

    /// <summary>Orbit a resolved block volume: its cells reflect/rotate, its height does not.</summary>
    private static BlockBox? TransformBox(BlockBox? box, SymmetryIntent sym, int k)
    {
        if (box is not { } source) return null;
        var (minX, minZ, maxX, maxZ) =
            Symmetry.CellRect(source.MinX, source.MinZ, source.MaxX, source.MaxZ, sym.Mode, sym.CenterX, sym.CenterZ, k);
        return new BlockBox(minX, source.MinY, minZ, maxX, source.MaxY, maxZ);
    }

    /// <summary>The anchor of a goal's image: its footprint turned cell by cell and the anchor read back off it,
    /// as the plan compiler fans one, so an even-sided structure reverses its lean.</summary>
    private static Pt TransformAnchor(Pt anchor, SymmetryIntent sym, int k, (int Width, int Depth) footprint)
    {
        var (x, z) = ObjectiveFootprint.ImageAnchor(
            anchor.X, anchor.Z, sym.Mode, sym.CenterX, sym.CenterZ, k, footprint.Width, footprint.Depth);
        return new Pt(x, anchor.Y, z);
    }

    private static (int Width, int Depth) DestroyableFootprint(string style)
    {
        var (width, _, depth) = ObjectiveFootprint.Destroyable(
            DestroyableStyles.TryParse(style, out var parsed) ? parsed : ObjectiveDefaults.Style);
        return (width, depth);
    }

    /// <summary>The image of a coordinate naming a block. A whole number is the block's index and lands on the
    /// index of the block's image; a half is the block's middle and turns as a point does. A turn that swaps the
    /// axes carries each axis's kind with it.</summary>
    private static Pt TransformBlock(Pt p, SymmetryIntent sym, int k)
    {
        bool wholeX = p.X == Math.Floor(p.X), wholeZ = p.Z == Math.Floor(p.Z);
        var (x, z) = Symmetry.Point(wholeX ? p.X + 0.5 : p.X, wholeZ ? p.Z + 0.5 : p.Z,
                                    sym.Mode, sym.CenterX, sym.CenterZ, k);
        var swaps = Math.Abs(Symmetry.Point(1, 0, sym.Mode, 0, 0, k).X) < 0.5;
        var (imageWholeX, imageWholeZ) = swaps ? (wholeZ, wholeX) : (wholeX, wholeZ);
        return new Pt(imageWholeX ? x - 0.5 : x, p.Y, imageWholeZ ? z - 0.5 : z);
    }

    // ── geometry (exact reflect/rotate, matching PGM MirroredRegion) ──────────────────────────────────
    // The reflection itself is exact float arithmetic for every mode (n² ∈ {1,2} ⇒ the 2/n² cancels;
    // rotations are coordinate swaps). A position — where a player stands, a rectangle's edge — turns as a
    // point; what names a block turns as that block (TransformBlock, TransformBox, TransformAnchor). A
    // rectangle's bounds live on the 1×1 block grid, so TransformRect snaps corners to whole blocks — a drawn
    // 20×50 box stays exactly 20×50 through the orbit.
    private static Pt TransformPt(Pt p, SymmetryIntent sym, int k)
    {
        var (x, z) = Symmetry.Point(p.X, p.Z, sym.Mode, sym.CenterX, sym.CenterZ, k);
        return new Pt(x, p.Y, z);
    }

    private static Rect TransformRect(Rect r, SymmetryIntent sym, int k)
    {
        var (nx, nz, xx, xz) = Symmetry.Rect(r.MinX, r.MinZ, r.MaxX, r.MaxZ, sym.Mode, sym.CenterX, sym.CenterZ, k);
        return new Rect(Math.Round(nx), Math.Round(nz), Math.Round(xx), Math.Round(xz));
    }

    // Transform a Minecraft yaw by running its facing vector through the linear part of the symmetry op
    // (origin 0). Yaw 0 faces +Z (south); facing = (-sin, cos), inverse yaw = atan2(-x, z). Unlike the
    // coordinate transforms this goes through atan2, so round away the float noise (90.00000001 → 90).
    private static double TransformYaw(double yaw, SymmetryIntent sym, int k)
    {
        var rad = yaw * Math.PI / 180.0;
        var (tx, tz) = Symmetry.Point(-Math.Sin(rad), Math.Cos(rad), sym.Mode, 0, 0, k);
        var deg = Math.Atan2(-tx, tz) * 180.0 / Math.PI;
        return Math.Round(((deg % 360) + 360) % 360, 1);
    }

    // The walls a room opens through, as the edges their outward normals point at once turned by the linear
    // part of the symmetry op. Order is kept, because the first door is the one a room is measured from.
    private static List<StatedDoor> TransformDoors(List<StatedDoor> doors, SymmetryIntent sym, int k) =>
        [.. doors.Select(door => door.Fanned(sym.Mode, sym.CenterX, sym.CenterZ, k))];

    // ── helpers ─────────────────────────────────────────────────────────────────────────
    private static bool IsKnown(string mode) => SymmetryModes.Fanned.Contains(mode);

    private static int IndexOfTeam(List<TeamDef> teams, string id)
    {
        for (var i = 0; i < teams.Count; i++) if (teams[i].Id == id) return i;
        return -1;
    }

    private static string RemapTeam(List<TeamDef> teams, string id, int k)
    {
        var i = IndexOfTeam(teams, id);
        return i < 0 ? id : teams[(i + k) % teams.Count].Id;
    }
}
