using System.Runtime.CompilerServices;
using PgmStudio.Contracts;
using PgmStudio.Export;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Services;

/// <summary>What the 3-D preview answers about one built world: the layout's own findings, every column's runs,
/// and the goal-placement and structure-site findings read off the build.</summary>
internal sealed record SketchPreview(Findings Layout, WorldColumnsDto Columns, Findings Goals, Findings Sites);

/// <summary>
/// The 3-D preview's answers, kept for as long as the world they were read from is kept.
///
/// <para>Keyed on the world instance, the way <see cref="EyeRenders"/> keeps its pictures: <see cref="BuiltWorlds"/>
/// hands every asker of one layout and intent the same world, and lets it go when it falls out, so an answer
/// here goes with it and a changed board starts with none. Asking again for a board already previewed reads
/// the answer rather than checking the layout and walking every column a second time.</para>
/// </summary>
internal static class SketchPreviews
{
    private static readonly ConditionalWeakTable<BuiltWorld, Lazy<SketchPreview>> Worlds = new();

    /// <summary>The preview of <paramref name="world"/> — <paramref name="read"/>'s, the first time it is
    /// asked.</summary>
    public static SketchPreview Of(BuiltWorld world, Func<SketchPreview> read) =>
        Worlds.GetValue(world, _ => new Lazy<SketchPreview>(read, LazyThreadSafetyMode.ExecutionAndPublication)).Value;
}
