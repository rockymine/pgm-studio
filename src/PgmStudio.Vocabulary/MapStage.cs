namespace PgmStudio.Vocabulary;

/// <summary>
/// A map's lifecycle stage. Stored on the
/// <c>map</c> row (set on origination, advanced as the map moves forward) so the dashboard can
/// list each stage on its own and a user can resume a draft. <c>Sketch</c> = geometry being drawn;
/// <c>Configure</c> = world has geometry, its <c>map.xml</c> is being authored; <c>Edit</c> = a map
/// with a finished <c>map.xml</c>, which no tool opens at its stage.
/// </summary>
public static class MapStage
{
    public const string Sketch = "sketch";
    public const string Configure = "configure";
    public const string Edit = "edit";

    /// <summary>The three, in the order a map moves through them.</summary>
    public static readonly string[] All = [Sketch, Configure, Edit];

    public static bool IsValid(string? stage) => stage is Sketch or Configure or Edit;
}
