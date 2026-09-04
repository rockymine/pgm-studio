namespace PgmStudio.Vocabulary;

/// <summary>
/// What a dressing placement is. The word is the <b>type discriminator</b> a placement is deserialized by —
/// each kind is its own record with its own fields, and <c>PlacedProp</c>'s <c>[JsonDerivedType]</c>
/// attributes name these constants, so the hierarchy and this set cannot say different things.
///
/// <para>Three parties spell them: the pass that places each kind, the picker that offers them, and the
/// refusal that lists them when a document names one that is not here.</para>
/// </summary>
public static class PropKinds
{
    /// <summary>A band of surface along a drawn line. It repaints the top course of what it crosses and adds
    /// no cell.</summary>
    public const string Stroke = "stroke";

    /// <summary>A channel or pool. The one kind that changes the ground rather than the surface.</summary>
    public const string Water = "water";

    /// <summary>Scattered greenery — grass, flowers, the ground's own cover.</summary>
    public const string Flora = "flora";

    /// <summary>One tree, grown from a species and a wood.</summary>
    public const string Tree = "tree";

    /// <summary>One boulder, bedded into the ground it stands on.</summary>
    public const string Boulder = "boulder";

    /// <summary>One building, stamped from a room style.</summary>
    public const string House = "house";

    /// <summary>The six, in the order the pass places them.</summary>
    public static readonly string[] All = [Water, Stroke, Flora, Tree, Boulder, House];
}
