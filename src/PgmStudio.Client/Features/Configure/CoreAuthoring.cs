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
    public sealed record Defaults(int Lava, int LavaHeight, int Float, int Leak)
    {
        public static readonly Defaults Empty = new(3, 3, 6, 5);
    }

    /// <summary>The offered lava footprint a scanned casing is nearest to. A map the studio did not build
    /// carries whatever its own author made; an authored core is chosen from the four on offer, so a
    /// detection is read back as the one it fits rather than kept as a size nothing can now state.</summary>
    public static int NearestLava(int casingSize, int shell) => Math.Clamp(casingSize - 2 * Math.Max(1, shell), 2, 5);

    /// <summary>The same for its courses. An open casing gave up its cap, so the same lava stands under one
    /// less block of obsidian.</summary>
    public static int NearestLavaHeight(int casingHeight, int shell, bool openTop) =>
        Math.Clamp(casingHeight - (openTop ? 1 : 2) * Math.Max(1, shell), 2, 5);

    public sealed class Core
    {
        public string Owner = "";
        public string Name = "";
        public double AnchorX, AnchorY, AnchorZ;
        /// <summary>The lava's footprint and its courses — the two numbers an author states. The casing is
        /// derived from them, so a shell with nothing inside it cannot be written down.</summary>
        public int Lava, LavaHeight;
        public int Float, Leak;
        public bool OpenTop;
        /// <summary>The resolved casing volume; null on a plan-authored core the world build has not stamped yet.</summary>
        public BlockBox? Volume;
        /// <summary>Enclosed lava blocks the detector counted — evidence for the author, never persisted.
        /// A count, not the footprint <see cref="Lava"/> states.</summary>
        public int LavaBlocks;

        /// <summary>The obsidian the stated interior implies: one block of wall on every side, and a cap
        /// unless the top is open.</summary>
        public int Size => Lava + 2;
        public int Height => LavaHeight + (OpenTop ? 1 : 2);

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
                Lava = Ctx.I(c, "lava", Defaults.Empty.Lava),
                LavaHeight = Ctx.I(c, "lavaHeight", Defaults.Empty.LavaHeight),
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
            o["lava"] = core.Lava;
            o["lavaHeight"] = core.LavaHeight;
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
