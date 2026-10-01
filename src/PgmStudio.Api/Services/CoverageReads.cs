using System.Runtime.CompilerServices;
using System.Text.Json;
using PgmStudio.Analysis.Playability;
using PgmStudio.Export;
using PgmStudio.Geom;

namespace PgmStudio.Api.Services;

using Dict = Dictionary<string, object?>;

/// <summary>
/// A built board's coverage, read once and kept with the world it was read off.
///
/// <para>The read walks the whole board — a field per waypoint and a walk per pair of them — and its JSON and
/// its picture are two answers to that one walk. Everything it derives from is the world (its layout and its
/// intent, which <see cref="BuiltWorlds"/> keys a world on) and the map document the walk is judged by, so it is
/// kept per world and keyed on the document's own bytes: an edit to either is a new world or a new key, and no
/// stale answer is served. A world let go takes its coverage with it.</para>
/// </summary>
public static class CoverageReads
{
    /// <summary>The documents a world is most often judged by at once — the stored one, and the one a read of a
    /// changed map is asking about.</summary>
    private const int DocumentsPerWorld = 2;

    private static readonly ConditionalWeakTable<BuiltWorld, Remembered<GroundCoverage.Result>> Worlds = new();

    /// <summary>The coverage of <paramref name="world"/> judged by <paramref name="doc"/> — <paramref name="read"/>'s,
    /// the first time it is asked.</summary>
    public static GroundCoverage.Result Of(BuiltWorld world, Dict doc, Func<GroundCoverage.Result> read) =>
        Worlds.GetValue(world, _ => new Remembered<GroundCoverage.Result>(DocumentsPerWorld))
              .Of(JsonSerializer.SerializeToUtf8Bytes(doc), read);
}
