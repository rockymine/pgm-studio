using System.Text.Json;
using System.Text.Json.Nodes;

namespace PgmStudio.Pgm.Authoring;

/// <summary>
/// What survives replacing a map's intent with a freshly compiled one.
///
/// <para>A plan states the map's <b>structure</b> — teams, spawns, wools, build zones, the stamped
/// structures — and rebuilding is how a change to the plan reaches the map, so the compiler's version of
/// those wins by design. The question is what a plan cannot state, because that is what a plain replace
/// deletes: the map's <b>authors and contributors</b>. The compiler writes <c>"authors": []</c>, so a rebuild
/// that carried nothing across would take the credits off every map that has been through Configure.</para>
///
/// <para>The carried set is deliberately just that, and the two slices left out are the interesting
/// half:</para>
/// <list type="bullet">
/// <item><b><c>islandTeams</c> is not carried</b> — it is not an author's record, it is a derivation the
/// compile endpoint pre-fills from the geometry it just built, which Configure may then correct. Island
/// ids are positional (the 1-based order <c>IslandDetector</c> walks the footprint in), so once the board
/// changes a stored assignment may name a different island than the one it was made about. Carrying it
/// would not preserve a decision; it would relabel territory at random.</item>
/// <item><b><c>symmetry</c> is not carried</b> — a compiled intent has none <em>on purpose</em>. It is
/// already fanned across the orbit, and <see cref="SymmetryExpander"/> runs whenever the field is set. Its
/// spawn/wool fill would be a no-op on a complete intent, but the expander rebuilds the intent from a fixed
/// set of properties and does not carry <c>Structures</c>, <c>Destroyables</c>, <c>Cores</c> or
/// <c>IslandTeams</c> through — so setting the field on a compiled intent would delete the wall, room-floor
/// and iron directives it exists to deliver.</item>
/// </list>
///
/// <para>Written down rather than inferred from whatever happens to be null: read the other way round, this
/// is the list of things a rebuild is not an answer to, and the reasons are what keep it that short.</para>
/// </summary>
public static class IntentCarry
{
    /// <summary>Keys inside <c>meta</c> that belong to the author rather than the plan. <c>name</c> is
    /// deliberately absent: the plan owns the map's identity, which is what lets renaming a plan rename the
    /// map it builds.</summary>
    public static readonly string[] AuthoredMetaKeys = ["authors", "contributors"];

    /// <summary>
    /// A freshly compiled intent with the stored intent's authored slices carried onto it. Every structural
    /// slice comes from <paramref name="compiledJson"/>; only the keys above ride across, and only where the
    /// compiled intent has nothing to say — an empty list as much as an absent key, since the compiler
    /// states <c>authors</c> and leaves it empty rather than omitting it.
    /// </summary>
    public static string CarryAuthored(string compiledJson, string? storedJson)
    {
        if (string.IsNullOrWhiteSpace(storedJson)) return compiledJson;
        JsonNode? compiled, stored;
        try { compiled = JsonNode.Parse(compiledJson); stored = JsonNode.Parse(storedJson); }
        catch (JsonException) { return compiledJson; }
        if (compiled is not JsonObject target || stored is not JsonObject source) return compiledJson;
        if (source["meta"] is not JsonObject storedMeta) return compiledJson;

        // A compiled intent always carries a meta (it holds the plan's name), so the object is made only for
        // the pathological case of one that does not.
        if (target["meta"] is not JsonObject targetMeta) target["meta"] = targetMeta = new JsonObject();

        var carried = false;
        foreach (var key in AuthoredMetaKeys)
            if (IsEmpty(targetMeta[key]) && !IsEmpty(storedMeta[key]))
            {
                targetMeta[key] = storedMeta[key]!.DeepClone();
                carried = true;
            }

        return carried ? target.ToJsonString() : compiledJson;
    }

    // Nothing to say: absent, null, an empty array, or an empty object. The distinction the compiler forces
    // is between an absent key and an empty one — it states the key and leaves it empty — so presence alone
    // cannot decide.
    private static bool IsEmpty(JsonNode? node) => node switch
    {
        null => true,
        JsonArray array => array.Count == 0,
        JsonObject obj => obj.Count == 0,
        _ => false,
    };
}
