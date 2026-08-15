using System.Collections;
using System.Reflection;
using PgmStudio.Domain;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Pgm.Tests;

using Dict = Dictionary<string, object?>;
using PgmStudio.Geom;

/// <summary>
/// What orbit-fill must <b>not</b> lose.
///
/// <para><see cref="SymmetryExpander.Expand"/> rebuilt the intent by naming its fields, so every slice added
/// to <see cref="MapIntent"/> after it was written was silently deleted: a map with a confirmed symmetry
/// exported with no destroyables, no cores, no island teams and none of the plan's stamped structures, and
/// nothing reported it. The first test below is written against the <em>type</em> rather than against a list
/// of field names, so the next slice cannot be dropped the same way — a property nobody taught the expander
/// about fails here instead of vanishing from someone's map.</para>
/// </summary>
public sealed class SymmetryExpanderCarryTests
{
    private static List<TeamDef> TwoTeams() =>
    [
        new() { Id = "red-team", Color = "red", Name = "Red" },
        new() { Id = "blue-team", Color = "blue", Name = "Blue" },
    ];

    /// <summary>An intent with every slice populated — the fixture the carry test measures against.</summary>
    private static MapIntent Full() => new()
    {
        Teams = TwoTeams(),
        MaxPlayers = 8,
        Symmetry = new SymmetryIntent { Mode = "rot_180", CenterX = 0, CenterZ = 0 },
        Meta = new MetaIntent { Name = "Carry" },
        Spawns = [new SpawnIntent { Team = "red-team", Point = new Pt(10, 12, 10) }],
        Observer = new ObserverIntent { Point = new Pt(0, 60, 0) },
        Build = new BuildIntent
        {
            MaxHeight = 40, Areas = [new Rect(0, 0, 10, 10)], Holes = [new Rect(4, 4, 6, 6)],
            VoidEnforcement = new VoidEnforcementIntent { Exclusions = [new Rect(1, 1, 3, 3)] },
        },
        WaterLanes = new WaterLaneIntent { Rects = [new Rect(20, 20, 24, 30)] },
        Wools = [new WoolIntent { Owner = "red-team", Color = "lime", Spawn = new Pt(5, 12, 5) }],
        Destroyables = [new DestroyableIntent
        {
            Owner = "red-team", Name = "Red Monument", Style = "cube-4", Materials = "emerald block",
            Anchor = new Pt(30, 12, 30), Float = 4, Box = new BlockBox(29, 16, 29, 32, 19, 32),
        }],
        Cores = [new CoreIntent
        {
            Owner = "red-team", Anchor = new Pt(40, 12, 40), Size = 7, Height = 4, Shell = 2,
            OpenTop = true, Float = 6, Leak = 9, Box = new BlockBox(37, 18, 37, 43, 21, 43),
        }],
        IslandTeams = new Dictionary<string, string> { ["1"] = "red-team" },
        Structures = new StructureIntent { IronCubes = [new IronCube(4, 4, true)] },
    };

    /// <summary>Whether a property value carries anything at all — null, or an empty collection, is a loss.</summary>
    private static bool IsPopulated(object? value) => value switch
    {
        null => false,
        ICollection collection => collection.Count > 0,
        _ => true,
    };

    [Test]
    public async Task No_slice_of_the_intent_survives_the_expansion_empty()
    {
        var expanded = SymmetryExpander.Expand(Full());

        var lost = typeof(MapIntent).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => !IsPopulated(property.GetValue(expanded)))
            .Select(property => property.Name)
            .ToList();

        await Assert.That(lost).IsEmpty();
    }

    [Test]
    public async Task No_property_of_the_orbited_build_intent_is_lost()
    {
        // The same trap on a smaller stage: OrbitBuild used to rebuild BuildIntent by naming
        // MaxHeight/Areas/Holes, so VoidEnforcement — added after that method was written — would have
        // been silently dropped on every symmetric map the moment it existed. Proof this fails on the old
        // behaviour: a `new BuildIntent { MaxHeight = b.MaxHeight, Areas = …, Holes = … }` rebuild (what
        // OrbitBuild did before this field existed) leaves VoidEnforcement at its default (null), which
        // IsPopulated reports as lost.
        var expanded = SymmetryExpander.Expand(Full());

        var lost = typeof(BuildIntent).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => !IsPopulated(property.GetValue(expanded.Build)))
            .Select(property => property.Name)
            .ToList();

        await Assert.That(lost).IsEmpty();
    }

    [Test]
    public async Task Void_enforcement_exclusions_orbit_like_build_areas()
    {
        var expanded = SymmetryExpander.Expand(Full());
        var exclusions = expanded.Build!.VoidEnforcement!.Exclusions;

        await Assert.That(exclusions.Count).IsEqualTo(2);   // the authored rect plus its rot_180 partner
        await Assert.That(exclusions).Contains(new Rect(1, 1, 3, 3));
        await Assert.That(exclusions).Contains(new Rect(-3, -3, -1, -1));   // rot_180 about the origin
    }

    [Test]
    public async Task A_core_authored_once_is_fair_for_both_teams()
    {
        // The map's whole premise: an author states one goal and the orbit gives the other team the same one.
        // A pair whose casings differ is not a symmetric map, so every knob rides across unchanged.
        var expanded = SymmetryExpander.Expand(Full());
        var cores = expanded.Cores!;

        await Assert.That(cores.Count).IsEqualTo(2);
        await Assert.That(cores.Select(core => core.Owner)).IsEquivalentTo(new[] { "red-team", "blue-team" });

        var mirrored = cores.Single(core => core.Owner == "blue-team");
        await Assert.That(mirrored.Size).IsEqualTo(7);
        await Assert.That(mirrored.Height).IsEqualTo(4);
        await Assert.That(mirrored.Shell).IsEqualTo(2);
        await Assert.That(mirrored.OpenTop).IsTrue();
        await Assert.That(mirrored.Float).IsEqualTo(6);
        await Assert.That(mirrored.Leak).IsEqualTo(9);
        await Assert.That(mirrored.DigDepth).IsEqualTo(3);

        // rot_180 about the origin: the anchor and the casing box both invert.
        await Assert.That((mirrored.Anchor.X, mirrored.Anchor.Z)).IsEqualTo((-40d, -40d));
        await Assert.That(mirrored.Box).IsEqualTo(new BlockBox(-43, 18, -43, -37, 21, -37));
    }

    [Test]
    public async Task A_destroyable_authored_once_is_fair_for_both_teams()
    {
        var expanded = SymmetryExpander.Expand(Full());
        var destroyables = expanded.Destroyables!;

        await Assert.That(destroyables.Count).IsEqualTo(2);
        var mirrored = destroyables.Single(d => d.Owner == "blue-team");
        await Assert.That(mirrored.Style).IsEqualTo("cube-4");
        await Assert.That(mirrored.Materials).IsEqualTo("emerald block");
        await Assert.That(mirrored.Float).IsEqualTo(4);
        await Assert.That(mirrored.Box).IsEqualTo(new BlockBox(-32, 16, -32, -29, 19, -29));
        // PGM rejects a nameless destroyable, and one name across both reads as one goal.
        await Assert.That(mirrored.Name).IsEqualTo("Blue Monument");
    }

    [Test]
    public async Task An_authored_partner_is_never_overwritten()
    {
        // Author overrides win: a map whose second core was corrected by hand keeps the correction.
        var intent = Full();
        var mine = intent.Cores![0] with { Owner = "blue-team", Size = 3, Anchor = new Pt(-1, 12, -1) };
        var expanded = SymmetryExpander.Expand(intent with { Cores = [.. intent.Cores, mine] });

        await Assert.That(expanded.Cores!.Count).IsEqualTo(2);
        await Assert.That(expanded.Cores.Single(core => core.Owner == "blue-team").Size).IsEqualTo(3);
    }

    [Test]
    public async Task Both_objectives_reach_the_generated_document()
    {
        // The end the loss was measured at: a symmetric map with one authored core and one authored
        // destroyable exported neither, because the expander deleted them before any generator ran.
        var doc = new Dict();
        IntentGenerator.Apply(doc, Full());

        await Assert.That(((List<object?>)doc["cores"]!).Count).IsEqualTo(2);
        await Assert.That(((List<object?>)doc["destroyables"]!).Count).IsEqualTo(2);
    }
}
