using System.Text.Json.Nodes;

namespace PgmStudio.Client.Features.Configure;

using Ctx = AuthoringContext;
using PgmStudio.Client.Components;
using PgmStudio.Geom;

// The core slice (intent.cores) the two Cores steps (Objectives / Casing) share, the wool slice's shape with
// a smaller model: a core is defended by one team and breached by every other, so there is nothing to map
// per capturing team — no monuments, no colour, no room. What a wool spends four steps on, a core states in
// an anchor and six casing numbers.
//
// The BOX is the field that decides whether anything is emitted at all. CoreGenerator writes the core's
// <region> straight from it (OB8) and skips a core that has none, so an imported world's confirmed
// suggestion carries the casing volume the detector measured. A plan-authored core has no box until the
// world-export path stamps it, which is why the box rides through untouched instead of being invented here.
public static class CoreAuthoring
{
    /// <summary>The casing numbers the generator defaults when a field is unauthored. Served by
    /// <c>GET /core-suggestions</c> so the one definition (<c>ObjectiveDefaults</c>) is not copied here.</summary>
    public sealed record Defaults(int Size, int Height, int Shell, int Float, int Leak)
    {
        public static readonly Defaults Empty = new(5, 5, 1, 6, 5);
    }

    public sealed class Core
    {
        public string Owner = "";
        public string Name = "";
        public double AnchorX, AnchorY, AnchorZ;
        public int Size, Height, Shell, Float, Leak;
        public bool OpenTop;
        /// <summary>The resolved casing volume; null on a plan-authored core the world build has not stamped yet.</summary>
        public BlockBox? Volume;
        /// <summary>Enclosed lava blocks the detector counted — evidence for the author, never persisted.</summary>
        public int Lava;

        /// <summary>How many blocks players must dig under the casing before its lava can leak (DC2).</summary>
        public int DigDepth => CoreDig.Depth(Leak, Float);
    }

    public static List<Core> ParseCores(JsonObject intent)
    {
        var cores = new List<Core>();
        if (intent["cores"] is not JsonArray arr) return cores;
        foreach (var c in arr.OfType<JsonObject>())
        {
            var anchor = c["anchor"] as JsonObject;
            cores.Add(new Core
            {
                Owner = Ctx.S(c, "owner"),
                Name = Ctx.S(c, "name"),
                AnchorX = Ctx.D(anchor, "x"), AnchorY = Ctx.D(anchor, "y"), AnchorZ = Ctx.D(anchor, "z"),
                Size = Ctx.I(c, "size", Defaults.Empty.Size),
                Height = Ctx.I(c, "height", Defaults.Empty.Height),
                Shell = Ctx.I(c, "shell", Defaults.Empty.Shell),
                Float = Ctx.I(c, "float", Defaults.Empty.Float),
                Leak = Ctx.I(c, "leak", Defaults.Empty.Leak),
                OpenTop = Ctx.B(c, "openTop"),
                Volume = ParseBox(c["box"] as JsonObject),
            });
        }
        return cores;
    }

    public static void WriteCores(JsonObject intent, IEnumerable<Core> cores)
    {
        // Owner + anchor column is a core's identity: one team may defend more than one, and the casing step
        // edits the numbers without moving it. Keys any entry the compiler wrote (piece/at) onto the rewrite.
        var carry = IntentSlice.Carrier(intent, "cores", Identity);

        intent["cores"] = new JsonArray(cores.Select(core =>
        {
            var o = carry(Key(core.Owner, core.AnchorX, core.AnchorZ));
            o["owner"] = core.Owner;
            o["name"] = core.Name;
            o["anchor"] = new JsonObject { ["x"] = core.AnchorX, ["y"] = core.AnchorY, ["z"] = core.AnchorZ };
            o["size"] = core.Size;
            o["height"] = core.Height;
            o["shell"] = core.Shell;
            o["float"] = core.Float;
            o["leak"] = core.Leak;
            o["openTop"] = core.OpenTop;
            o["box"] = core.Volume is { } box
                ? new JsonObject
                {
                    ["minX"] = box.MinX, ["minY"] = box.MinY, ["minZ"] = box.MinZ,
                    ["maxX"] = box.MaxX, ["maxY"] = box.MaxY, ["maxZ"] = box.MaxZ,
                }
                : null;
            return (JsonNode)o;
        }).ToArray());
    }

    private static string? Identity(JsonObject entry)
    {
        var anchor = entry["anchor"] as JsonObject;
        return Key(Ctx.S(entry, "owner"), Ctx.D(anchor, "x"), Ctx.D(anchor, "z"));
    }

    private static string Key(string owner, double x, double z)
        => $"{owner}|{Math.Floor(x)}|{Math.Floor(z)}";

    private static BlockBox? ParseBox(JsonObject? box) => box is null
        ? null
        : new BlockBox(Ctx.I(box, "minX"), Ctx.I(box, "minY"), Ctx.I(box, "minZ"),
                  Ctx.I(box, "maxX"), Ctx.I(box, "maxY"), Ctx.I(box, "maxZ"));
}
