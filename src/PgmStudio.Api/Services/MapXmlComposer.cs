using PgmStudio.Pgm;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Api.Services;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Composes a PGM <c>map.xml</c> string from a map document: <c>Deserializer.FromDict</c> →
/// <c>MapXml</c> → <c>XmlWriter.ToXml</c>. Intent-authored maps additionally get the standard CTW
/// boilerplate (<see cref="CtwStandards"/>), spawn-ore renewables (<see cref="ResourceRenewables"/>), and
/// the no-void apply-rule ordered last. Shared by the XML and world-export endpoints.
/// </summary>
public static class MapXmlComposer
{
    public static string Compose(Dict doc, bool isIntent, IReadOnlySet<int>? surfaceBlockIds,
        IReadOnlyList<(string Type, int X, int Y, int Z)> resources)
    {
        var mx = Deserializer.FromDict(doc);
        if (isIntent)
        {
            CtwStandards.Apply(mx, surfaceBlockIds);
            ResourceRenewables.Apply(mx, resources);

            // The not-build-area "no-void" rule must decide last (PGM stops at the first applicator).
            var voidRules = mx.ApplyRules.Where(r => r.RegionId == "not-build-area").ToList();
            if (voidRules.Count > 0)
            {
                mx.ApplyRules.RemoveAll(r => r.RegionId == "not-build-area");
                mx.ApplyRules.AddRange(voidRules);
            }
        }
        return XmlWriter.ToXml(mx);
    }
}
