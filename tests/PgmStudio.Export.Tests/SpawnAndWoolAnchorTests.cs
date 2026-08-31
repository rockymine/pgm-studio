using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Export.Tests;

/// <summary>
/// <b>Where a spawn and a wool actually end up.</b> <c>PlanCompiler</c> writes a Y onto both markers, and it
/// is the piece's plan-space <c>Surface</c> — the flat nominal ground, the number the relief solve then
/// abandons. Read as the answer, that is a bug: it is the same mistake the build ceiling made, and it is
/// what <c>B222</c> was filed over.
///
/// <para>It is not the answer. The world build resolves both anchors against the terrain it has just
/// laid — <c>FrameFloor</c> over the room's own footprint — exactly the way <c>B128</c> resolved the
/// destroyable and core anchors, and the carried number never reaches the XML. What was missing was anyone
/// saying so: <c>ResolveGoalAnchor</c> carries a paragraph explaining it for goals, and the spawn and wool
/// sites carried nothing, so the code read like a defect it had already stopped being.</para>
///
/// <para>These two tests are that paragraph made checkable. The first shows the resolved Y is <b>derived</b>
/// (it moves when the ground moves); the second shows it is <b>not the carried one</b> (it does not move when
/// only that number does). Either alone would pass on a version that still read the plan's Y.</para>
/// </summary>
public sealed class SpawnAndWoolAnchorTests
{
    // The two rooms sit apart on x, because mirror_z fans each piece across z=0: laid on the same x they
    // would land on each other's ground and the higher surface would decide both floors, which measures the
    // fixture rather than the rule.
    private static PlanModel PlanAt(int spawnSurface, int woolSurface) => PlanModel.Parse($$"""
        { "plan":2, "globals":{"symmetry":"mirror_z","surface":9,"cell":5},
          "pieces":[
            {"id":"spawn-a","role":"spawn","rect":[-8,-14,6,6],"surface":{{spawnSurface}}},
            {"id":"lane","role":"lane","rect":[-8,-8,16,16],"surface":20},
            {"id":"wool-a","role":"wool-room","rect":[2,8,6,6],"surface":{{woolSurface}}}
          ],
          "placements":{
            "spawns":[{"piece":"spawn-a","at":[2.5,2.5],"facing":"front"}],
            "wools":[{"piece":"wool-a","at":[2.5,2.5]}]
          } }
        """)!;

    private static (double Spawn, double Wool) Built(PlanModel plan, MapIntent? substitute = null)
    {
        var (layout, intent) = PlanCompiler.Compile(plan);
        var built = WorldBuilder.Build(layout.ToJson(), substitute ?? intent);
        return (built.ResolvedIntent.Spawns[0].Point.Y, built.ResolvedIntent.Wools![0].Spawn.Y);
    }

    /// <summary>Raise the ground the rooms stand on and both anchors rise with it. This is what makes them
    /// derived rather than constant — and it is the half that would fail if the world build ever stopped
    /// measuring and started trusting a stored number.</summary>
    [Test]
    public async Task Both_anchors_follow_the_ground_the_world_build_laid()
    {
        var low = Built(PlanAt(spawnSurface: 12, woolSurface: 12));
        var high = Built(PlanAt(spawnSurface: 30, woolSurface: 24));

        await Assert.That(high.Spawn - low.Spawn).IsEqualTo(18);   // 30 − 12
        await Assert.That(high.Wool - low.Wool).IsEqualTo(12);     // 24 − 12
    }

    /// <summary>And neither reads the Y the compiler carried. The plan is compiled, then the number on both
    /// markers is replaced with a lie before the world is built — same XZ, same pieces, same ground — and
    /// nothing moves. A version that resolved a spawn from <c>piece.Surface</c> would answer 9 here.</summary>
    [Test]
    public async Task Neither_anchor_reads_the_nominal_height_the_compiler_carried()
    {
        var plan = PlanAt(spawnSurface: 30, woolSurface: 24);
        var (_, honest) = PlanCompiler.Compile(plan);

        var lying = honest with
        {
            Spawns = [.. honest.Spawns.Select(s => new SpawnIntent
            {
                Team = s.Team, Point = new Pt(s.Point.X, 9, s.Point.Z), Protection = s.Protection,
                Piece = s.Piece, Iron = s.Iron, Yaw = s.Yaw,
            })],
            Wools = [.. honest.Wools!.Select(w => new WoolIntent
            {
                Owner = w.Owner, Color = w.Color, Room = w.Room, Piece = w.Piece, Entries = w.Entries,
                Spawn = new Pt(w.Spawn.X, 9, w.Spawn.Z),
            })],
        };

        await Assert.That(Built(plan, lying)).IsEqualTo(Built(plan));
    }
}
