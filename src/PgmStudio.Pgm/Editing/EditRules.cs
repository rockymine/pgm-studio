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
    /// <summary>A reference the document cannot resolve. An apply-rule or a filter naming a region or a filter
    /// that is not in the registry and is not a builtin, or a filter naming itself — which would be a cycle of
    /// one. The wiring is what makes a PGM map mean anything, so a rule pointing at nothing is a rule that
    /// silently never fires.</summary>
    /// <remarks>Create the region or filter first and wire the rule to it afterwards — <c>GET /api/map/{slug}</c> carries the filter registry and <c>GET /api/map/{slug}/regions</c> the regions. A filter that would reference itself wants the child it was meant to wrap instead.</remarks>
    public const string UnresolvedReference = "ED1";

    /// <summary>An edit the document is not in a state to take. The payload is well-formed and everything it
    /// names exists; what it would produce cannot mean anything — a group with fewer children than its type
    /// takes, a compound with no children to remove one from, an apply-rule carrying no region, filter or
    /// action, a build-void enforcement with no build region to enforce.</summary>
    /// <remarks>The edit is refused rather than the document: read the thing it names — <c>GET /api/map/{slug}/regions/tree</c> for a compound's children, <c>GET /api/map/{slug}</c> for the rule stack — and either give the edit the parts it is missing or apply it to something that can take it.</remarks>
    public const string Inapplicable = "ED2";
}
