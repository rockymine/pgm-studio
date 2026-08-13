namespace PgmStudio.Pgm.Authoring;

using PgmStudio.Domain;
using PgmStudio.Geom;

/// <summary>
/// Orbit-fill for the declarative intent (docs/contracts/new-map-authoring.md §3). Given an intent whose
/// <see cref="MapIntent.Symmetry"/> is set, the author only authors <b>one orbit unit</b> — team 0's
/// spawn, one wool — and this expander rotates/reflects it onto the other teams so the rest of the map
/// configuration falls out for free. The expansion happens at the <i>intent</i> level (before any
/// generator runs): it returns a new <see cref="MapIntent"/> whose <c>Spawns</c>/<c>Wools</c> cover every
/// team, which the slice generators then project unchanged.
/// <para><b>Orbit ordering.</b> <see cref="MapIntent.Teams"/> are taken to be listed <i>in orbit order</i>:
/// step <c>k</c> of the symmetry maps the unit owned by <c>Teams[i]</c> onto <c>Teams[(i+k) % n]</c>.
/// For a clean symmetric map the team count equals the symmetry order (<c>rot_90</c>→4, mirrors/<c>rot_180</c>→2)
/// so each team is filled exactly once.</para>
/// <para><b>Author overrides win.</b> A team that already has an authored spawn/wool is never overwritten —
/// only missing teams are filled. So the author can hand-correct any orbit member after the fill.</para>
/// <para>Geometry uses the canonical <see cref="Symmetry"/> transforms. Build areas <i>are</i> orbited too
/// (the author draws the bridges on one side; <see cref="OrbitBuild"/> mirrors each onto the other sides and
/// dedups any centre/axis piece) — but, unlike spawns/wools, they carry no per-team identity, so they orbit
/// as a flat set of rects rather than a team-indexed fill.</para>
/// </summary>
public static class SymmetryExpander
{
    /// <summary>Suggested team count for a symmetry mode (§4): <c>rot_90</c>→4, everything else→2.</summary>
    public static int SuggestedTeamCount(string mode) => Order(mode);

    /// <summary>Return an intent with teams (synthesized if absent), spawns/wools orbit-filled across all
    /// teams, and build areas orbited. No-op (returns the input) when there's no symmetry or an unknown
    /// mode.</summary>
    public static MapIntent Expand(MapIntent intent)
    {
        if (intent.Symmetry is not { } sym || string.IsNullOrWhiteSpace(sym.Mode)) return intent;
        if (!IsKnown(sym.Mode)) return intent;

        var order = Order(sym.Mode);
        // Teams the author listed win; otherwise derive count from the symmetry and synthesize them (§4),
        // so "symmetry + one spawn" is a complete map. Orbit positions map onto this team list in order.
        var teams = intent.Teams is { Count: > 0 } provided ? provided : SynthesizeTeams(intent, order);
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
        };
    }

    // ── team synthesis ──────────────────────────────────────────────────────────────────
    // No teams listed → derive count from the symmetry order and assign palette colours. Team 0's id is
    // anchored to whatever the author authored against (their one spawn/wool) so the orbit attaches to it.
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
        if (build is not { } b || b.Areas.Count == 0) return build;
        return new BuildIntent { MaxHeight = b.MaxHeight, Areas = OrbitRects(b.Areas, sym, order), Holes = OrbitRects(b.Holes, sym, order) };
    }

    // Lanes orbit exactly as build areas do — same rects, same dedup — so a lane drawn on one side opens on
    // every side at once. A lane straddling the axis maps onto itself and collapses to one.
    private static WaterLaneIntent? OrbitWaterLanes(WaterLaneIntent? lanes, SymmetryIntent sym, int order)
    {
        if (lanes is not { Rects.Count: > 0 } l) return lanes;
        return new WaterLaneIntent { Rects = OrbitRects(l.Rects, sym, order) };
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
    private static List<SpawnIntent> FillSpawns(List<SpawnIntent> authored, List<TeamDef> teams, SymmetryIntent sym, int order)
    {
        var result = new List<SpawnIntent>(authored);
        var have = new HashSet<string>(authored.Select(s => s.Team));
        foreach (var src in authored)
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
                    Point = TransformPt(src.Point, sym, k),
                    Protection = src.Protection.Select(r => TransformRect(r, sym, k)).ToList(),
                    Yaw = TransformYaw(src.Yaw, sym, k),
                });
            }
        }
        return result;
    }

    // ── wools ───────────────────────────────────────────────────────────────────────────
    private static List<WoolIntent>? FillWools(List<WoolIntent>? authored, List<TeamDef> teams, SymmetryIntent sym, int order)
    {
        if (authored is null) return null;
        var result = new List<WoolIntent>(authored);
        var have = new HashSet<string>(authored.Select(w => w.Owner));
        foreach (var src in authored)
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
                    Color = "",   // orbit copies default to the new owner team's colour (WoolGenerator.ColorSlug)
                    Room = src.Room.Select(r => TransformRect(r, sym, k)).ToList(),
                    Spawn = TransformPt(src.Spawn, sym, k),
                    Monuments = [.. src.Monuments.Select(m => m with
                    {
                        // shift the capturing team by the same orbit step; transform its capture block
                        Team = RemapTeam(teams, m.Team, k),
                        Location = TransformPt(m.Location, sym, k),
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
        var result = new List<DestroyableIntent>(authored);
        var have = new HashSet<string>(authored.Select(d => d.Owner));
        foreach (var src in authored)
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
                    // PGM rejects a nameless destroyable, and one name for both teams reads as one goal —
                    // so the copy takes its new owner's, the same form the plan compiler mints.
                    Name = $"{owner.Name} Monument",
                    Anchor = TransformPt(src.Anchor, sym, k),
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
        var result = new List<CoreIntent>(authored);
        var have = new HashSet<string>(authored.Select(core => core.Owner));
        foreach (var src in authored)
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
                    Anchor = TransformPt(src.Anchor, sym, k),
                    Box = TransformBox(src.Box, sym, k),
                });
            }
        }
        return result;
    }

    /// <summary>Orbit a resolved block volume: its footprint reflects/rotates, its height does not.</summary>
    private static BlockBox? TransformBox(BlockBox? box, SymmetryIntent sym, int k)
    {
        if (box is not { } source) return null;
        var (minX, minZ, maxX, maxZ) =
            Symmetry.Rect(source.MinX, source.MinZ, source.MaxX, source.MaxZ, sym.Mode, sym.CenterX, sym.CenterZ, k);
        return new BlockBox((int)Math.Round(minX), source.MinY, (int)Math.Round(minZ),
                            (int)Math.Round(maxX), source.MaxY, (int)Math.Round(maxZ));
    }

    // ── geometry (exact reflect/rotate, matching PGM MirroredRegion) ──────────────────────────────────
    // The reflection itself is exact float arithmetic for every mode (n² ∈ {1,2} ⇒ the 2/n² cancels;
    // rotations are coordinate swaps). Each kind keeps its corpus grid: a spawn/point sits at a block
    // CENTRE (x.5) or a block anchor (integer) — TransformPt preserves whichever exactly (snapping to a
    // single grid would corrupt the other); a rectangle's bounds live on the 1×1 block grid, so
    // TransformRect snaps corners to whole blocks — a drawn 20×50 box stays exactly 20×50 through the orbit.
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

    // ── helpers ─────────────────────────────────────────────────────────────────────────
    private static int Order(string mode) => mode == "rot_90" ? 4 : 2;

    private static bool IsKnown(string mode) =>
        mode is "rot_90" or "rot_180" or "mirror_x" or "mirror_z" or "mirror_d1" or "mirror_d2";

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
