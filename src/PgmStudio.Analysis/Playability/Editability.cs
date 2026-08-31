using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Prepared;
using PgmStudio.Analysis.Region;
using PgmStudio.Vocabulary;

namespace PgmStudio.Analysis.Playability;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Which columns of a map a player may edit, and what makes each one editable — the same shape of pass as
/// traversability, over the same grid, answering <see cref="EditZone"/> per column.
///
/// <para><b>PGM's own resolution, followed rather than approximated.</b> A block edit walks the map's
/// region-filter applications in priority order and stops at the <b>first</b> one whose region holds the
/// block and whose filter does not abstain; nothing matching leaves the edit standing. So the order is
/// document order, the first answer wins, and a map grants building by not forbidding it.</para>
///
/// <para><b>Place and break are two walks, not one.</b> They are separate scopes to PGM
/// (<c>block</c> names both, <c>block-place</c> and <c>block-break</c> one each), and the difference is
/// exactly where the interesting faults live: a canopy hanging over the void is unplaceable-on and, where
/// the map carved the exception for it, still breakable. A column is judged by the more permissive of the
/// two, because a player who can do either can edit it.</para>
///
/// <para><b>Void is a question about the column, not about the rule.</b> PGM's <c>&lt;void/&gt;</c> matches a
/// block with only air below it — literally <c>y == 0 || air at (x, 0, z)</c> — so a void filter answers
/// differently per column and needs the Y=0 layer to read at all. Without a scan there is no such layer and
/// <see cref="Result.HasY0"/> is false, which is a fact about the map's state rather than a verdict on it.</para>
/// </summary>
public static class Editability
{
    /// <summary>The three attributes an apply rule states a block filter under. <c>block</c> is both scopes;
    /// the other two are one each, which is what lets a map deny placing over the void while leaving what
    /// already hangs there breakable.</summary>
    private const string BlockBoth = "block", BlockPlace = "block_place", BlockBreak = "block_break";

    /// <summary>What a filter says about one column: nothing (the walk goes on), yes, no, or "yes for
    /// somebody" — a material or team filter, which permits without settling who.</summary>
    private enum Say { Abstain, Allow, Deny, Conditional }

    /// <summary>A block rule's filter, read once: what kind of thing it is and whether it is inverted. The
    /// polarity is kept because <c>&lt;void/&gt;</c> and <c>not(void)</c> are opposite answers to the same
    /// question and collapsing them loses which one the map wrote.</summary>
    private readonly record struct Verdict(string Kind, bool Negated);

    /// <param name="IsVoid">Whether each column is void to PGM — no block at y=0 — which is the question a
    /// void filter asks and the one a placement over nothing has to answer. Meaningless where
    /// <paramref name="HasY0"/> is false, since without a scan there is no layer to read it from.</param>
    public sealed record Result(
        int MinX, int MinZ, int MaxX, int MaxZ, int Width, int Height,
        byte[] Zone, bool[] IsVoid, Dictionary<string, int> Counts, bool HasY0)
    {
        /// <summary>Whether the column holding a world position is void. Null off the grid, which is a
        /// position outside the analysed box rather than one over nothing.</summary>
        public bool? VoidAt(double x, double z)
        {
            int ix = (int)Math.Floor(x) - MinX, iz = (int)Math.Floor(z) - MinZ;
            if (!HasY0 || ix < 0 || iz < 0 || ix >= Width || iz >= Height) return null;
            return IsVoid[iz * Width + ix];
        }

        /// <summary>Whether the column at index <paramref name="i"/> is one a player may build across —
        /// editable, and by something the map granted rather than by ground that is already there. Bridging
        /// ungranted ground would let a walk cross void nobody may bridge.</summary>
        public bool Bridgeable(int i) =>
            Zone[i] == EditZone.IndexOf(EditZone.BuildZone) || Zone[i] == EditZone.IndexOf(EditZone.Filtered);

        public string ZoneAt(int i) => EditZone.All[Zone[i]];
    }

    /// <summary>What a block filter is, as far as a column-level read can tell: <c>never</c>, <c>allow</c>,
    /// a void test, or something conditional. <paramref name="negated"/> comes back true through an odd
    /// number of <c>not</c> wrappers; a <c>deny</c> wrapper is folded into <c>never</c>'s answer for the case
    /// it matches and abstains otherwise, which is handled by the caller.</summary>
    private static Verdict Classify(string value, Dict filters, HashSet<string>? seen = null, bool negated = false)
    {
        seen ??= [];
        if (string.IsNullOrEmpty(value) || seen.Contains(value)) return new("other", negated);
        if (value == "never") return new("never", negated);
        if (value is "always" or "allow") return new("allow", negated);
        // An inline expression the serializer wrote rather than a named filter: deny(void) is the corpus
        // idiom for "no bridging out there", and it denies over void and abstains everywhere else.
        if (value == "deny(void)") return new("deny-void", negated);
        if (value.Contains("void") && !filters.ContainsKey(value)) return new("void", !negated);

        if (filters.GetValueOrDefault(value) is not Dict filter) return new("other", negated);
        var type = filter.GetValueOrDefault("type") as string;
        if (type == "void") return new("void", negated);
        if (type == "never") return new("never", negated);

        var next = new HashSet<string>(seen) { value };
        var child = filter.GetValueOrDefault("child") as string ?? "";
        if (type == "not") return Classify(child, filters, next, !negated);
        if (type == "deny")
        {
            var inner = Classify(child, filters, next, negated);
            return inner.Kind == "void" && !inner.Negated ? new("deny-void", negated) : new("other", negated);
        }
        if (type == "allow") return Classify(child, filters, next, negated);
        if (type is "any" or "all" or "one")
        {
            var kinds = MapDoc.AsList(filter.GetValueOrDefault("children"))
                .Select(c => Classify(c as string ?? "", filters, next, negated)).ToList();
            if (kinds.Count == 0) return new("other", negated);
            // A composite is the void test only when every term is that same test; one that merely holds a
            // void term is conditional, because the other terms are what answer the columns it does not. The
            // corpus break filter — any(all(leaves|log, void), not(void)) — is exactly that shape, and
            // reading it as a plain void denial is what hides the exception it exists to carve.
            if (kinds.All(k => k.Kind == "void" && k.Negated == kinds[0].Negated)) return kinds[0];
            if (kinds.All(k => k.Kind == "never")) return new("never", negated);
        }
        return new("other", negated);
    }

    /// <summary>What one rule's filter says about one column, given whether that column is void.</summary>
    private static Say SayFor(Verdict verdict, bool isVoid) => verdict.Kind switch
    {
        "never" => verdict.Negated ? Say.Allow : Say.Deny,
        "allow" => verdict.Negated ? Say.Deny : Say.Allow,
        // not(void): allow on ground, deny over void — and the reverse through another negation.
        "void" => (isVoid ^ verdict.Negated) ? Say.Allow : Say.Deny,
        // deny(void): deny over void, abstain on ground, because <deny> answers nothing when it does not match.
        "deny-void" => isVoid ? Say.Deny : Say.Abstain,
        _ => Say.Conditional,
    };

    public static (int minX, int minZ, int maxX, int maxZ) RegionBbox(Dict data, int margin)
    {
        var xs = new List<double>();
        var zs = new List<double>();
        foreach (var region in MapDoc.AsDict(data.GetValueOrDefault("regions")).Values.OfType<Dict>())
        {
            if (MapDoc.AsDict(region.GetValueOrDefault("bounds_2d")) is { Count: > 0 } bounds)
            {
                var low = MapDoc.AsDict(bounds.GetValueOrDefault("min"));
                var high = MapDoc.AsDict(bounds.GetValueOrDefault("max"));
                if (MapDoc.Num(low.GetValueOrDefault("x")) is { } lowX && MapDoc.Num(low.GetValueOrDefault("z")) is { } lowZ
                    && MapDoc.Num(high.GetValueOrDefault("x")) is { } highX && MapDoc.Num(high.GetValueOrDefault("z")) is { } highZ)
                { xs.Add(lowX); xs.Add(highX); zs.Add(lowZ); zs.Add(highZ); }
            }
        }
        if (xs.Count == 0) return (-64, -64, 64, 64);
        return ((int)xs.Min() - margin, (int)zs.Min() - margin, (int)xs.Max() + margin, (int)zs.Max() + margin);
    }

    public static Result Compute(Dict data, HashSet<(int, int)>? y0Columns,
        (int minX, int minZ, int maxX, int maxZ)? bbox = null, int margin = 16)
    {
        var regions = MapDoc.AsDict(data.GetValueOrDefault("regions"));
        var filters = MapDoc.AsDict(data.GetValueOrDefault("filters"));
        var rules = MapDoc.AsList(data.GetValueOrDefault("apply_rules")).OfType<Dict>().ToList();

        var (minX, minZ, maxX, maxZ) = bbox ?? RegionBbox(data, margin);
        int nx = maxX - minX, nz = maxZ - minZ;
        var cells = nx * nz;
        var boundsD = ((double)minX, (double)minZ, (double)maxX, (double)maxZ);

        var hasY0 = y0Columns is not null;
        var isVoid = new bool[cells];
        if (hasY0)
        {
            Array.Fill(isVoid, true);                    // void everywhere…
            foreach (var (x, z) in y0Columns!)           // …except columns with a Y=0 block
            {
                int ix = x - minX, iz = z - minZ;
                if (ix >= 0 && ix < nx && iz >= 0 && iz < nz) isVoid[iz * nx + ix] = false;
            }
        }

        // Two walks per column — place and break — each stopping at the first rule that answers. A rule
        // grants the ground it explicitly names; `granted` records that so a build zone can be told from
        // ground that is merely nobody's business.
        var place = new Say[cells];
        var breakage = new Say[cells];
        var granted = new bool[cells];

        bool[]? Mask(object? reference)
        {
            if (reference is null) { var everywhere = new bool[cells]; Array.Fill(everywhere, true); return everywhere; }
            return RegionMask(reference, regions, boundsD, minX, minZ, nx, nz);
        }

        foreach (var rule in rules)
        {
            var inRegion = Mask(rule.GetValueOrDefault("region"));
            if (inRegion is null) continue;

            foreach (var attribute in (ReadOnlySpan<string>)[BlockBoth, BlockPlace, BlockBreak])
            {
                if (rule.GetValueOrDefault(attribute) is not string stated || stated.Length == 0) continue;
                var touchesPlace = attribute is BlockBoth or BlockPlace;
                var touchesBreak = attribute is BlockBoth or BlockBreak;

                // "you may only edit what is inside region X" — a permission with a spatial condition rather
                // than a filter, so the gate's own footprint is the grant and everything else is refused.
                if (regions.ContainsKey(stated))
                {
                    var gate = Mask(stated);
                    if (gate is null) continue;
                    for (var i = 0; i < cells; i++)
                    {
                        if (!inRegion[i]) continue;
                        var say = gate[i] ? Say.Allow : Say.Deny;
                        if (gate[i]) granted[i] = true;
                        if (touchesPlace) Settle(place, i, say);
                        if (touchesBreak) Settle(breakage, i, say);
                    }
                    continue;
                }

                var verdict = Classify(stated, filters, null);
                // A void rule states the build zone as its own complement: "you may not edit the void OUT
                // THERE" is how a map says "the zone is IN HERE". So the ground this rule does not cover is
                // the grant, and nothing else in the document names it.
                var statesVoid = verdict.Kind is "void" or "deny-void";
                for (var i = 0; i < cells; i++)
                {
                    if (!inRegion[i])
                    {
                        if (statesVoid) granted[i] = true;
                        continue;
                    }
                    var say = SayFor(verdict, isVoid[i]);
                    if (say == Say.Allow && !statesVoid) granted[i] = true;
                    if (touchesPlace) Settle(place, i, say);
                    if (touchesBreak) Settle(breakage, i, say);
                }
            }
        }

        var zone = new byte[cells];
        var openIndex = (byte)EditZone.IndexOf(EditZone.BuildZone);
        var groundIndex = (byte)EditZone.IndexOf(EditZone.Ground);
        var filteredIndex = (byte)EditZone.IndexOf(EditZone.Filtered);
        var sealedIndex = (byte)EditZone.IndexOf(EditZone.Sealed);
        for (var i = 0; i < cells; i++)
        {
            // The more permissive of the two scopes decides: a player who can break here can edit here.
            var open = place[i] is Say.Allow or Say.Abstain || breakage[i] is Say.Allow or Say.Abstain;
            var conditional = place[i] == Say.Conditional || breakage[i] == Say.Conditional;
            zone[i] = open ? (granted[i] ? openIndex : groundIndex)
                    : conditional ? filteredIndex
                    : sealedIndex;
        }

        var counts = EditZone.All.ToDictionary(word => word, word => zone.Count(z => z == EditZone.IndexOf(word)));
        return new Result(minX, minZ, maxX, maxZ, nx, nz, zone, isVoid, counts, hasY0);
    }

    /// <summary>Record a rule's answer for one column, first answer winning. Only an abstention leaves the
    /// walk open, which is PGM's own rule: the first application that does not abstain settles the edit.</summary>
    private static void Settle(Say[] walk, int i, Say say)
    {
        if (say != Say.Abstain && walk[i] == Say.Abstain) walk[i] = say;
    }

    /// <summary>One region's footprint over a grid, as a cell mask — the rasterization every apply-rule reader
    /// shares, so an enter rule and a block rule cannot disagree about which cells a region covers. Null where
    /// the reference resolves to no geometry.</summary>
    internal static bool[]? RegionMask(object? reference, Dict regions,
        (double, double, double, double) bounds, int minX, int minZ, int nx, int nz)
    {
        var region = reference is string named ? regions.GetValueOrDefault(named) as Dict : reference as Dict;
        var geometry = RegionGeometry2d.ToGeometry(region, bounds, regions);
        if (geometry is null || geometry.IsEmpty) return null;
        var prepared = PreparedGeometryFactory.Prepare(geometry);
        var mask = new bool[nx * nz];
        for (var iz = 0; iz < nz; iz++)
        for (var ix = 0; ix < nx; ix++)
            mask[iz * nx + ix] = prepared.CoversCell(minX + ix, minZ + iz);
        return mask;
    }
}
