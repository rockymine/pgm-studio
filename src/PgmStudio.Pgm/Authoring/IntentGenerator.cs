namespace PgmStudio.Pgm.Authoring;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Top-level declarative generator (docs/contracts/new-map-authoring.md): projects a whole
/// <see cref="MapIntent"/> into the PGM document by applying each slice in order. The single entry
/// point the intent endpoint calls; slices stay in their own focused generators.
/// <para>Symmetry is applied <b>by default</b> first (<see cref="SymmetryExpander"/>): when the intent
/// carries a <see cref="SymmetryIntent"/>, the authored orbit unit is rotated/reflected onto the other
/// teams so the slices project a complete, symmetric map. No symmetry → the intent passes through as-is.</para>
/// </summary>
public static class IntentGenerator
{
    public static void Apply(Dict doc, MapIntent intent)
    {
        intent = SymmetryExpander.Expand(intent);   // orbit-fill the authored unit across all teams (§4)
        MetaGenerator.Apply(doc, intent);
        TeamsGenerator.Apply(doc, intent);
        WoolGenerator.Apply(doc, intent);
        DestroyableGenerator.Apply(doc, intent);
        // Build last: its broad not-build-area "no-void" rule ALLOWs editing any solid block, and PGM stops
        // at the first apply rule that decides — so it must come *after* the spawn/wool-room protections
        // (which sit outside the build area) or it short-circuits them. Matches docs/template.xml ordering.
        BuildGenerator.Apply(doc, intent);
    }
}
