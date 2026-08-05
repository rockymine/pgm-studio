using System.Text.Json.Serialization;
using PgmStudio.Geom.Algorithms;

namespace PgmStudio.Minecraft.Dressing;

/// <summary>
/// One thing an author placed on the map. Dressing is authored, not sprinkled: a tree is cover and a boulder
/// is a wall, so <em>where</em> each one stands is a decision about how the map plays and belongs to the person
/// making the map. A prop therefore carries its own position and its own knobs, and the pass places exactly
/// what is here and nothing else.
///
/// <para>The two kinds of geometry are the two ways a decision can be shaped. A <b>point</b> prop stands
/// somewhere (<see cref="TreeProp"/>, <see cref="BoulderProp"/>); an <b>area</b> prop covers a stretch
/// (<see cref="PathProp"/> along a line, <see cref="FloraProp"/> inside a ring). Within an area the placement
/// is still a noise field, because nobody wants to place nine hundred blades of grass — but the area itself
/// was drawn.</para>
///
/// <para>Every prop is fanned across the symmetry orbit. An author draws one half of a map and gets a fair
/// one, which is the same contract the layout itself has had all along.</para>
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(PathProp), "path")]
[JsonDerivedType(typeof(TreeProp), "tree")]
[JsonDerivedType(typeof(BoulderProp), "boulder")]
[JsonDerivedType(typeof(FloraProp), "flora")]
public abstract record PlacedProp
{
    /// <summary>Stable id, so a canvas can select, move and delete one prop among many.</summary>
    public string Id { get; init; } = "";

    /// <summary>The seed every field this prop rolls is keyed on. Two props of the same kind and knobs at
    /// different seeds differ; the same prop always re-exports identically.</summary>
    public uint Seed { get; init; }
}

/// <summary>A route across the ground: the line the author drew and how wide a strip of it is paved. A path
/// replaces the surface it crosses rather than adding to it — it is a finish, not terrain — which is why it
/// carries blocks rather than a height, and why nothing grows on what it covers.</summary>
public sealed record PathProp : PlacedProp
{
    /// <summary>The drawn centerline, as <c>[x, z]</c> pairs. Two points or more.</summary>
    public IReadOnlyList<double[]> Points { get; init; } = [];

    /// <summary>Half the paved width, in blocks.</summary>
    public double Radius { get; init; } = 3;

    public PathStyle Style { get; init; } = PathStyle.Solid;

    /// <summary>0–1; what a <see cref="PathStyle.Worn"/> path keeps. Every other style paves its whole band.</summary>
    public double Coverage { get; init; } = 0.7;

    /// <summary>The blocks the path is paved with. One is a plain surface; several are what a
    /// <see cref="PathStyle.Cobble"/> path tiles between, which is the only style that spends more than the
    /// first.</summary>
    public IReadOnlyList<PaveBlock> Blocks { get; init; } = [new PaveBlock(Minecraft.Blocks.Gravel, 0)];
}

/// <summary>One block a path may be paved with.</summary>
public readonly record struct PaveBlock(int Id, int Data);

/// <summary>
/// One tree, standing where it was placed — and one of <b>two</b> trees, which <see cref="Form"/> picks.
///
/// <para>A <see cref="TreeForm.Template"/> tree is vanilla: <see cref="Species"/> names its wood, its canopy
/// profile and its proportions, and <see cref="Height"/> scales the lot. A <see cref="TreeForm.Grown"/> tree
/// is the recursive skeleton: <see cref="Wood"/> names what it is cut from and the knobs below shape it. Each
/// form reads only its own fields, so the ones it does not read are inert rather than wrong — the same way a
/// <see cref="PathProp"/> spends <see cref="PathProp.Coverage"/> only when it is worn.</para>
/// </summary>
public sealed record TreeProp : PlacedProp
{
    public int X { get; init; }
    public int Z { get; init; }

    public TreeForm Form { get; init; } = TreeForm.Template;

    /// <summary>Template only — a row in <see cref="DressingPalette.Species"/>: the wood, the canopy profile
    /// and the proportions of one vanilla species.</summary>
    public string Species { get; init; } = "oak";

    /// <summary>Grown only — a row in <see cref="DressingPalette.Woods"/>. A grown tree's shape is the
    /// author's, so its wood is all that is left to name.</summary>
    public string Wood { get; init; } = "oak";

    /// <summary>Overall height in blocks. Template: it scales the species' proportions. Grown: not a uniform
    /// scale — a smaller tree also carries a thinner stem and fewer branches, so a sapling reads as a sapling
    /// rather than as a shrunken tree.</summary>
    public double Height { get; init; } = 12;

    /// <summary>Grown only — 1–3 stems at the base.</summary>
    public int Stems { get; init; } = 1;

    /// <summary>Grown only — how far the central axis climbs: low spreads, high spires.</summary>
    public double Leader { get; init; } = 0.55;

    /// <summary>Grown only — how much the trunk wanders on its way up.</summary>
    public double Flow { get; init; } = 0.45;

    /// <summary>Grown only — how far a branch leaves its parent, in radians.</summary>
    public double BranchAngle { get; init; } = 0.55;

    /// <summary>Grown only — branching depth: 2 is a tree, 3 a denser one.</summary>
    public int Levels { get; init; } = 2;

    /// <summary>Grown only — how big each tip's leaf cluster is.</summary>
    public double LeafSize { get; init; } = 0.6;

    /// <summary>This tree's growth parameters, as the grower wants them. Read only when it is grown.</summary>
    public TreeShape Shape => new(
        Height: Math.Max(5, Height), Stems: Math.Clamp(Stems, 1, 3), Levels: Math.Clamp(Levels, 2, 3),
        BranchAngle: BranchAngle, Flow: Flow, Leader: Leader);

    /// <summary>The blocks this tree is made of, whichever form it is: a template takes its species' wood, a
    /// grown tree the one it was given.</summary>
    public TreeWood Timber => Form == TreeForm.Template
        ? DressingPalette.SpeciesNamed(Species).Wood
        : DressingPalette.WoodNamed(Wood);
}

/// <summary>One boulder, half-buried where it was placed.</summary>
public sealed record BoulderProp : PlacedProp
{
    public int X { get; init; }
    public int Z { get; init; }
    public BoulderForm Form { get; init; } = BoulderForm.Round;

    /// <summary>How far the rock reaches from its centre, in blocks.</summary>
    public double Size { get; init; } = 2.5;

    public int BlockId { get; init; } = Minecraft.Blocks.Stone;
    public int BlockData { get; init; }

    /// <summary>Whether moss creeps onto the sky-lit faces — the rock's own micro-flora.</summary>
    public bool Mossy { get; init; } = true;
}

/// <summary>A stretch of ground that grows cover. The ring is drawn; what fills it is the density field of
/// <see cref="FloraSpec"/>, because placing every blade by hand is not authoring, it is data entry.</summary>
public sealed record FloraProp : PlacedProp
{
    /// <summary>The drawn outline, as <c>[x, z]</c> pairs. Three points or more.</summary>
    public IReadOnlyList<double[]> Points { get; init; } = [];

    public FloraSpec Spec { get; init; } = new();
}
