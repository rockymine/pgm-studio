using System.Text.Json.Nodes;

namespace PgmStudio.Client.Features.Configure;

using Ctx = AuthoringContext;
using PgmStudio.Geom;

// The destroyable slice (intent.destroyables) the Destroyables phase edits — the core slice's shape with the
// casing swapped for the three knobs a DTM goal actually has: what shape it is, what it is built of, and how
// far it floats over the ground solved under its anchor. A destroyable is defended by one team and broken by
// every other, so like a core there is nothing to map per attacking team: no monuments, no colour, no room.
//
// The BOX decides whether the goal's <region> comes from the world or from the build. DestroyableGenerator
// emits it verbatim (OB8) so the blocks and the region that scopes them are the one box the stamper used; a
// confirmed suggestion carries the mass the detector measured, and a plan-authored destroyable has none until
// the world-export path stamps one, which is why it rides through untouched rather than being invented here.
public static class DestroyableAuthoring
{
    /// <summary>What the generator starts a hand-placed destroyable as. Served by
    /// <c>GET /destroyable-suggestions</c> so <c>ObjectiveDefaults</c> is the one definition rather than a
    /// copy on this side of the wire.</summary>
    public sealed record Defaults(string Style, string Materials, int Float,
                                  IReadOnlyList<string> StyleOptions, IReadOnlyList<string> MaterialOptions)
    {
        /// <summary>What the step shows before the route answers. Named as <c>DestroyableDefaultsDto</c>
        /// names them, so the two halves of one answer cannot drift apart.</summary>
        public static readonly Defaults Empty =
            new("pillar-3", "obsidian", 4,
                ["pillar-1", "pillar-2", "pillar-3", "cube-3", "cube-4", "column-plus"],
                ["obsidian", "emerald block", "gold block", "ender stone"]);
    }

    public sealed class Destroyable
    {
        public string Owner = "";
        public string Name = "";
        public double AnchorX, AnchorY, AnchorZ;

        public string Style = Defaults.Empty.Style;
        public string Materials = Defaults.Empty.Materials;
        public int Float = Defaults.Empty.Float;

        /// <summary>The resolved block volume — a confirmed suggestion's mass, or null until the export
        /// stamps one.</summary>
        public BlockBox? Volume;

        /// <summary>Why the scan proposed it, carried so the row that offered it can still say so after it is
        /// accepted. Null for one placed by hand.</summary>
        public int? SameNearby;
        public int? Elevation;
        public int? Blocks;

        /// <summary>Whether this came off the scan rather than the author's own click. A proposal is right
        /// about two times in three, so the step marks which is which.</summary>
        public bool Detected => SameNearby is not null;
    }

    public static List<Destroyable> Parse(JsonObject intent)
    {
        var found = new List<Destroyable>();
        if (intent["destroyables"] is not JsonArray arr) return found;
        foreach (var d in arr.OfType<JsonObject>())
        {
            var anchor = d["anchor"] as JsonObject;
            found.Add(new Destroyable
            {
                Owner = Ctx.S(d, "owner"),
                Name = Ctx.S(d, "name"),
                AnchorX = Ctx.D(anchor, "x"), AnchorY = Ctx.D(anchor, "y"), AnchorZ = Ctx.D(anchor, "z"),
                Style = Ctx.S(d, "style") is { Length: > 0 } style ? style : Defaults.Empty.Style,
                Materials = Ctx.S(d, "materials") is { Length: > 0 } mat ? mat : Defaults.Empty.Materials,
                Float = Ctx.I(d, "float", Defaults.Empty.Float),
                Volume = ParseBox(d["box"] as JsonObject),
            });
        }
        return found;
    }

    public static void Write(JsonObject intent, IEnumerable<Destroyable> destroyables)
    {
        // Owner + anchor column is a destroyable's identity: one team may defend more than one, and the
        // structure step edits its knobs without moving it. Keys any entry the compiler wrote (piece / stamp)
        // onto the rewrite so Configure does not delete what it does not model (N13).
        var carry = IntentSlice.Carrier(intent, "destroyables", Identity);

        intent["destroyables"] = new JsonArray(destroyables.Select(d =>
        {
            var o = carry(Key(d.Owner, d.AnchorX, d.AnchorZ));
            o["owner"] = d.Owner;
            o["name"] = d.Name;
            o["anchor"] = new JsonObject { ["x"] = d.AnchorX, ["y"] = d.AnchorY, ["z"] = d.AnchorZ };
            o["style"] = d.Style;
            o["materials"] = d.Materials;
            o["float"] = d.Float;
            o["box"] = d.Volume is { } box
                ? new JsonObject
                {
                    ["minX"] = box.MinX, ["minY"] = box.MinY, ["minZ"] = box.MinZ,
                    ["maxX"] = box.MaxX, ["maxY"] = box.MaxY, ["maxZ"] = box.MaxZ,
                }
                : null;
            return (JsonNode)o;
        }).ToArray());
    }

    /// <summary>The name a destroyable takes when the author has not typed one. PGM rejects a nameless
    /// destroyable and the compiler auto-names from owner and index, so the step offers the same thing rather
    /// than leaving a required field blank.</summary>
    public static string DefaultName(string teamName, int index) =>
        index == 0 ? $"{teamName} Monument" : $"{teamName} Monument {index + 1}";

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
