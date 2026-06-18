namespace PgmStudio.Contracts;

/// <summary>
/// A map's lifecycle stage — the three editor surfaces it can be picked up in. Stored on the
/// <c>map</c> row (set on origination, advanced as the map moves forward) so the dashboard can
/// list each stage on its own and a user can resume a draft. <c>Sketch</c> = geometry being drawn;
/// <c>Configure</c> = world has geometry, its <c>map.xml</c> is being authored; <c>Edit</c> = a map
/// with a finished <c>map.xml</c> being refined.
/// </summary>
public static class MapStage
{
    public const string Sketch = "sketch";
    public const string Configure = "configure";
    public const string Edit = "edit";

    public static bool IsValid(string? stage) => stage is Sketch or Configure or Edit;
}
