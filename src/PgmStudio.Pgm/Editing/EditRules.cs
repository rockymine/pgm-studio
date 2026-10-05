using PgmStudio.Vocabulary;
namespace PgmStudio.Pgm.Editing;

/// <summary>
/// The two faults the document editors own, and the only two the rest of the studio has no id for.
///
/// <para>Six of the eight reasons an edit is refused belong to the request and are
/// <see cref="Domain.RequestRules"/>: an id naming nothing (<c>RQ4</c>), a value outside a closed set or a
/// required field absent (<c>RQ1</c>), an id already taken or a row something still holds (<c>RQ5</c>). These
/// two are the editors' because both are about the <b>document</b>: the payload is well-formed and names
/// things that exist, and the edit still cannot be applied to the map as it stands.</para>
/// </summary>
public static class EditRules
{
    /// <summary>An apply rule or a filter names a region or a filter that does not exist, or a filter names
    /// itself.</summary>
    /// <remarks>Either add the named region to <c>regions</c> or the named filter to <c>filters</c>, or change the
    /// reference to a region or filter that exists and is not the filter itself.</remarks>
    [Rule(RuleCategory.Unknown, RuleConcern.Request, RuleConcern.World)]
    public const string UnresolvedReference = "ED1";

    /// <summary>A compound region has fewer than 2 children, or a negative region fewer than 1.</summary>
    /// <remarks>Add a region to the <c>children</c> of the compound region.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Request, RuleConcern.World)]
    public const string Inapplicable = "ED2";

    /// <summary>An apply rule has no region, filter or action.</summary>
    /// <remarks>Either set the <c>region</c> of the apply rule to a region in <c>regions</c>, or set its
    /// <c>filter</c> to a filter in <c>filters</c>.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Request, RuleConcern.World)]
    public const string EmptyApplyRule = "ED3";

    /// <summary>A quarter-turn copy names a region that is not a rectangle, cuboid, cylinder, circle, sphere,
    /// point or block.</summary>
    /// <remarks>Send the request again with the region in its path set to a rectangle, cuboid, cylinder, circle,
    /// sphere, point or block.</remarks>
    [Rule(RuleCategory.Unsatisfiable, RuleConcern.Request, RuleConcern.World)]
    public const string QuarterTurnOfACompound = "ED4";
}
