using PgmStudio.Minecraft.Dressing;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Vocabulary;

namespace PgmStudio.Export;

/// <summary>
/// DR-TONE — every boulder cut from nothing but the tones of the ground it stands on.
///
/// <para>A rock reads as a rock by not being made of the field it sits in. <see cref="TerrainPalette"/> groups
/// blocks into <b>tone families</b> — the unit a pattern is filled from, and what a player reads at a distance
/// — so the question is asked in families rather than in blocks: a sandstone rock on sand is two different
/// blocks and one tone, and it has no silhouette at any size. A rock keeping one family the ground does not
/// have is a rock however much else it shares, which is why what fires is <em>built wholly from</em> the field
/// rather than touching it.</para>
///
/// <para>The ground a rock is judged against is the ground it can <b>rest</b> on
/// (<see cref="Materials.Resting"/>): a theme grading its surface by angle paints its steep faces bare rock,
/// nothing stands on a face — <c>DR-STEEP</c> is the rule that says so — and what a board paints there is not
/// what a rock standing on the meadow below meets. The bands under the theme's own cliff angle are, and they
/// are every band it grades, not only the shallowest: ground the middle band paints is ground.</para>
///
/// <para>Which theme is under a cell is <see cref="TerrainThemeScope"/>'s answer, so this rides the same
/// rasterize the ground reading already pays for and is asked at the same gate. A rock is judged where it was
/// <b>placed</b> rather than at every image of its orbit: the fault is in the recipe and the fix is one
/// decision, so the finding carries the cell an author can find it at.</para>
/// </summary>
public static class RockTone
{
    /// <summary>Every boulder whose every tone family the ground under it already states. Reported per
    /// placement, because the fix is that placement's recipe and a board states a handful of rocks.</summary>
    public static Findings Check(string layoutJson)
    {
        IReadOnlyList<PlacedProp> props;
        try { props = DressingScope.PropsOf(layoutJson); }
        catch (DressingParseException) { return Findings.None; }
        catch (System.Text.Json.JsonException) { return Findings.None; }

        var boulders = props.OfType<BoulderProp>().ToList();
        if (boulders.Count == 0) return Findings.None;

        var themeAt = TerrainThemeScope.ThemeAt(layoutJson);
        var layers = SketchLayout.Stack(SketchLayout.Parse(layoutJson));
        // Only a prop naming no layer needs the cell map, and one naming none is the rare case, so the second
        // rasterize is paid for only where it is asked for.
        var owners = new Lazy<IReadOnlyDictionary<(string Layer, int X, int Z), string>>(
            () => SketchRasterizer.ShapeThemeOwners(layoutJson));

        var findings = new List<Finding>();
        var at = 0;
        foreach (var boulder in boulders)
        {
            var subject = boulder.Id.Length > 0 ? boulder.Id : $"props[{at}]";
            at++;

            var rock = Families(Materials.BlocksOf(boulder.Style.Rock));
            if (rock.Count == 0) continue;

            var ground = Families(Materials.Resting(
                Ground(themeAt(Standing(boulder, layers, owners), boulder.X, boulder.Z))));
            if (ground.Count == 0 || !rock.IsSubsetOf(ground)) continue;

            findings.Add(new Finding(DressingRules.RockInTheGroundsTone,
                $"boulder '{subject}' at ({boulder.X}, {boulder.Z}) is cut from "
                + $"{Listed(rock)}, and the ground it stands on is {Listed(ground)} — every tone the rock is "
                + "made of is one the ground already has, so it reads as a patch of that ground standing up "
                + "rather than as a rock",
                Severity.Complaint, Field: "dressing.props", Subjects: [subject]));
        }
        return findings;
    }

    /// <summary>The material the top course of a theme's ground is written from — its surface bucket, or its
    /// fill where the surface does not paint, the fill being what every unclaimed block falls to.</summary>
    private static TerrainMaterial Ground(TerrainTheme theme) =>
        theme.Surface.Enabled ? theme.Surface.Material : theme.Fill;

    /// <summary>Which layer's paint a boulder stands in: the one it names, or — where it names none — the
    /// topmost layer with ground under it, which is the surface the pass seats it on.</summary>
    private static string Standing(
        BoulderProp boulder, IReadOnlyList<SketchLayer> layers,
        Lazy<IReadOnlyDictionary<(string Layer, int X, int Z), string>> owners)
    {
        if (boulder.Layer is { Length: > 0 } named) return named;
        for (var at = layers.Count - 1; at >= 0; at--)
            if (owners.Value.ContainsKey((layers[at].Id ?? "", boulder.X, boulder.Z))) return layers[at].Id ?? "";
        return layers.Count > 0 ? layers[^1].Id ?? "" : "";
    }

    /// <summary>The tone families a set of blocks belongs to. A block no family names — a fixture, a block
    /// outside the ground vocabulary — contributes none: it has no tone to share, and a rock made only of such
    /// blocks is not asked.</summary>
    private static HashSet<string> Families(IEnumerable<(int Id, int Data)> blocks)
    {
        var families = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (id, data) in blocks)
            if (TerrainPalette.FamilyOf(id, data) is { } family) families.Add(family);
        return families;
    }

    private static string Listed(HashSet<string> families) =>
        string.Join(" and ", families.OrderBy(name => name, StringComparer.Ordinal));
}
