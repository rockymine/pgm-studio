using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace PgmStudio.Client.Features.Configure;

/// <summary>
/// Rewriting one array slice of the stored intent without discarding the rest of it.
///
/// <para>A Configure step models a handful of an entry's fields — a spawn's point and yaw, a wool's colour
/// and monuments — and rebuilding the entry out of those alone deletes every key the step does not know
/// about. The plan compiler writes several such keys: a spawn's <c>piece</c> and <c>iron</c> (which size the
/// stamped spawn room and place its renewable ore), a wool's <c>piece</c> and <c>entries</c> (which size the
/// cage and cut its doors). Their loss is silent and shows up only in the exported world, where the spawn
/// falls back to the marker-anchored default shell and no ore is stamped at all.</para>
///
/// <para>So a step starts from the entry it is replacing and overwrites only what it owns. Carrying the
/// whole prior object rather than a named list of survivors is what makes that hold for the next key the
/// compiler learns to write, instead of repeating the fix per field.</para>
/// </summary>
internal static class IntentSlice
{
    /// <summary>
    /// A factory for the entries of <c>intent[sliceKey]</c>: given an entry's identity it returns a clone of
    /// the stored entry with that identity, or a fresh object when the slice has none. The caller sets its
    /// own fields on the result; every other key rides through untouched.
    /// </summary>
    public static Func<string, JsonObject> Carrier(
        JsonObject intent, string sliceKey, Func<JsonObject, string?> identity)
    {
        var prior = new Dictionary<string, JsonObject>();
        if (intent[sliceKey] is JsonArray entries)
            foreach (var entry in entries.OfType<JsonObject>())
                if (identity(entry) is { Length: > 0 } id)
                    prior.TryAdd(id, entry);
        return id => prior.TryGetValue(id, out var kept) ? (JsonObject)kept.DeepClone() : new JsonObject();
    }
}
