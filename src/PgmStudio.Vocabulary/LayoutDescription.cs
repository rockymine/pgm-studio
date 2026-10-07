using System.Globalization;

namespace PgmStudio.Vocabulary;

/// <summary>One run of a layout description: its words, and the glossary term it explains when it names one,
/// so a screen can link the run to <c>/glossary#{slug}</c>.</summary>
public sealed record DescriptionPart(string Text, string? Term = null);

/// <summary>
/// A generated layout in plain words, composed from what its descriptor and structure say: the team count and
/// size, the symmetry, the wools and the shapes of their approaches, the hub and the front line. The parts keep
/// the glossary words (<see cref="Glossary"/>) as linkable runs; <see cref="Plain"/> joins them into text.
/// </summary>
public static class LayoutDescription
{
    /// <summary>The layout as text and glossary links. A form the vocabulary has no words for is named as it is
    /// spelled, so a new shape reads as itself rather than being dropped.</summary>
    public static IReadOnlyList<DescriptionPart> Of(
        int playersPerTeam, int teams, string symmetry, int woolsPerTeam,
        IReadOnlyList<string> woolForms, string hub, string frontline)
    {
        var (low, high) = SizeBands.Players(SizeBands.Of(playersPerTeam));
        var range = SizeBands.Of(playersPerTeam) == SizeBands.Centi ? $"{low}+" : $"{low}–{high}";
        var single = woolsPerTeam == 1;

        List<DescriptionPart> parts =
        [
            new($"A {Number(teams)}-team layout for "),
            new($"{range} players a team", "size band"),
            new($", {SymmetryPhrase(symmetry)}. Each team has {(single ? "one" : Number(woolsPerTeam))} "),
            new(single ? "wool" : "wools", "wool"),
            new(single ? ", kept in a " : ", each kept in its own "),
            new("wool room", "wool room"),
            new(" and reached by "),
            new(ApproachForms(woolForms, woolsPerTeam), "approach"),
            new($". The team's side is built around {HubForm(hub)} "),
            new("hub", "hub"),
            new(", "),
            .. FrontLinePhrase(frontline),
            new("."),
        ];
        return Merge(parts);
    }

    /// <summary>The description as one string, with no links.</summary>
    public static string Plain(IReadOnlyList<DescriptionPart> parts) => string.Concat(parts.Select(part => part.Text));

    private static string Number(int count) => count switch
    {
        1 => "one",
        2 => "two",
        3 => "three",
        4 => "four",
        _ => count.ToString(CultureInfo.InvariantCulture),
    };

    private static string SymmetryPhrase(string symmetry) => symmetry switch
    {
        SymmetryModes.Rot180 => "copied by a half turn about the centre",
        SymmetryModes.Rot90 => "copied by quarter turns about the centre",
        SymmetryModes.MirrorX => "mirrored left to right",
        SymmetryModes.MirrorZ => "mirrored front to back",
        SymmetryModes.MirrorD1 or SymmetryModes.MirrorD2 => "mirrored across a diagonal",
        SymmetryModes.None => "with no symmetry",
        _ => $"laid out as {symmetry}",
    };

    /// <summary>The approach families as one noun phrase: "an L-shaped approach", "U-shaped approaches", "I-shaped
    /// and L-shaped approaches".</summary>
    private static string ApproachForms(IReadOnlyList<string> forms, int wools)
    {
        var shapes = forms.Distinct().Select(Shaped).ToList();
        var noun = wools == 1 ? "approach" : "approaches";
        return shapes.Count switch
        {
            0 => $"{(wools == 1 ? "an" : "its own")} {noun}",
            1 when wools == 1 => $"{shapes[0].Article} {shapes[0].Adjective} {noun}",
            _ => $"{string.Join(", ", shapes.Take(shapes.Count - 1).Select(shape => shape.Adjective))}"
                 + $"{(shapes.Count > 1 ? " and " : "")}{shapes[^1].Adjective} {noun}",
        };
    }

    private static (string Article, string Adjective) Shaped(string form) => form switch
    {
        "i" => ("an", "I-shaped"),
        "l" => ("an", "L-shaped"),
        "u" => ("a", "U-shaped"),
        "h" => ("an", "H-shaped"),
        "z" => ("a", "Z-shaped"),
        "donut" => ("a", "donut-shaped"),
        "clamp" => ("a", "clamp-shaped"),
        "scythe" => ("a", "scythe-shaped"),
        _ => ("a", $"{form}-shaped"),
    };

    private static string HubForm(string hub) => hub switch
    {
        "bar" => "a bar-shaped",
        "single" => "a single-piece",
        "twin" => "a twin",
        "ring" => "a ring-shaped",
        "g" => "a G-shaped",
        "p" => "a P-shaped",
        "double-hole" => "a two-holed",
        _ => $"a {hub}",
    };

    private static IEnumerable<DescriptionPart> FrontLinePhrase(string frontline)
    {
        if (frontline == "none")
        {
            yield return new("and the side has no ");
            yield return new("front line", "front line");
            yield return new(", so the hub meets the mid directly");
            yield break;
        }
        var form = frontline switch
        {
            "bar" => "a bar-shaped",
            "single" => "a single-piece",
            "twin" => "a twin",
            _ => $"a {frontline}",
        };
        yield return new($"with {form} ");
        yield return new("front line", "front line");
        yield return new(" facing the ");
        yield return new("mid", "mid");
    }

    private static List<DescriptionPart> Merge(List<DescriptionPart> parts)
    {
        List<DescriptionPart> merged = [];
        foreach (var part in parts)
        {
            if (merged.Count > 0 && merged[^1].Term is null && part.Term is null)
                merged[^1] = new DescriptionPart(merged[^1].Text + part.Text);
            else merged.Add(part);
        }
        return merged;
    }
}
