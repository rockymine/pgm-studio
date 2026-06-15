namespace PgmStudio.Pgm.Editing;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Top-level declarative generator (docs/contracts/new-map-authoring.md): projects a whole
/// <see cref="MapIntent"/> into the PGM document by applying each slice in order. The single entry
/// point the intent endpoint calls; slices stay in their own focused generators.
/// </summary>
public static class IntentGenerator
{
    public static void Apply(Dict doc, MapIntent intent)
    {
        TeamsGenerator.Apply(doc, intent);
        BuildGenerator.Apply(doc, intent);
        WoolGenerator.Apply(doc, intent);
    }
}
