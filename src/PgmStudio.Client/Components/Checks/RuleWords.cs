using System.Net;
using System.Text.RegularExpressions;
using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Components;

/// <summary>
/// The words the client puts on a rule: what a category asks of the author, a family's name and its place in
/// the order a map is made, a subject's label, how a layout rule is known, and a rule's text reduced to one
/// line or rendered inline. The server serves ids and categories; these are how an author reads them.
/// </summary>
public static partial class RuleWords
{
    /// <summary>A category as the action it asks for, with the line that says when it applies.</summary>
    public sealed record Action(RuleCategory Category, string Label, string Says, bool AuthorFixes);

    /// <summary>The eight categories in the order an author meets them; the last three are not the author's to
    /// fix, and a list draws a divider before them.</summary>
    public static readonly IReadOnlyList<Action> Actions =
    [
        new(RuleCategory.Malformed, "Fix the format", "The file or field can’t be read as written.", true),
        new(RuleCategory.Unknown, "Fix a name", "It names something that doesn’t exist.", true),
        new(RuleCategory.Conflict, "Choose which wins", "Two things you asked for disagree.", true),
        new(RuleCategory.Unsatisfiable, "Change the design", "What you asked for can’t all be true at once.", true),
        new(RuleCategory.Unplayable, "Make it playable", "The map builds, but players can’t play it as it is.", true),
        new(RuleCategory.Forbidden, "Ask for something else", "The studio doesn’t allow this.", false),
        new(RuleCategory.Unavailable, "Try again later", "Something the studio depends on didn’t answer.", false),
        new(RuleCategory.Internal, "Report it", "This is a fault in the studio, not in your map.", false),
    ];

    public static Action ActionOf(RuleCategory category) => Actions.First(action => action.Category == category);

    /// <summary>The families in the order a map is made — the request, the plan, its game settings, the sketch,
    /// the built world, its decoration — each under the name a list heads it with.</summary>
    private static readonly (string Family, string Name)[] Families =
    [
        ("RQ", "The request"), ("ED", "Document edits"), ("SR", "A map’s source"), ("IM", "Import"),
        ("PL", "Plan structure"), ("PC", "Piece contact"), ("WX", "Room frames"), ("CT", "Middle and contact"),
        ("SP", "Spawn"), ("WL", "Wool"), ("LN", "Lanes"), ("FR", "Front line"), ("MD", "The middle"),
        ("EL", "Elevation"), ("HB", "Hubs"), ("BZ", "Build zones"), ("G", "Globals"), ("GO", "Destroy goals"),
        ("OB", "Objectives"), ("DC", "Destroyables and cores"), ("SH", "Shops"),
        ("SK", "Sketch"), ("RL", "Terraform"), ("EZ", "Editable ground"), ("EX", "Export"),
        ("LB", "Library"), ("HS", "House style materials"), ("PT", "Palette materials"), ("ST", "Structures"),
        ("HP", "A placed building"), ("HJ", "How wings meet"), ("DR", "Decoration"),
    ];

    public static string FamilyName(string family) =>
        Families.FirstOrDefault(entry => entry.Family == family).Name ?? family;

    /// <summary>Where a family sits in the order a map is made; an unlisted one sorts last.</summary>
    public static int FamilyRank(string family)
    {
        var index = Array.FindIndex(Families, entry => entry.Family == family);
        return index < 0 ? Families.Length : index;
    }

    /// <summary>One question asked at every stage a map passes, each by its own rule: can a player get there.
    /// A rule on this ladder shows the others beside it.</summary>
    public static readonly IReadOnlyList<(string Rule, string Asks)> Reachability =
    [
        ("WX6", "One piece: can a door be cut into this room?"),
        ("PL9", "The whole plan: can a spawn reach this wool?"),
        ("EX1", "The built ground: is it all connected?"),
        ("DR-PASS", "One building: does it leave 8 blocks to get past?"),
        ("DR-SLOPE", "One building: is its footprint level enough?"),
    ];

    private static readonly Dictionary<string, string> TermLabels = new(StringComparer.Ordinal)
    {
        ["dead-share"] = "Ground on no route", ["fill-ratio"] = "Board that is land",
        ["route-interference"] = "Routes that cross", ["enclosed-void-count"] = "Enclosed holes",
        ["neutral-stepping-count"] = "Neutral stepping stones", ["team-stepping-count"] = "Team stepping stones",
        ["band-count"] = "Bands across the middle", ["isolation-cut-count"] = "Cuts that isolate ground",
        ["uncrossed-middle-void"] = "Middle gaps nobody crosses", ["frontline-count"] = "Front lines",
        ["frontline-width"] = "Front line width", ["max-chain-length"] = "Longest chain of pieces",
        ["lane-width"] = "Lane width", ["wool-wool-distance"] = "Wool to wool",
        ["spawn-wool-distance"] = "Spawn to wool", ["spawn-wool-spread"] = "Spread of spawn-to-wool walks",
        ["wool-front-distance"] = "Wool to front line", ["wool-front-balance"] = "Wool-to-front balance",
        ["spawn-wool-ratio"] = "Spawn-to-wool ratio", ["wool-front-ratio"] = "Wool-to-front ratio",
        ["wool-front-remoteness"] = "Wool remoteness from the front", ["goal-spawn-ratio"] = "Goal-to-spawn ratio",
        ["own-goal-distance"] = "Own goal distance", ["opposing-goal-distance"] = "Opposing goal distance",
        ["goal-spawn-distance"] = "Goal to spawn",
    };

    /// <summary>The terms measured as a share of the board, which read as a percentage.</summary>
    private static readonly HashSet<string> Shares = new(StringComparer.Ordinal) { "dead-share", "fill-ratio", "route-interference" };

    /// <summary>A scored term as an author says it; the id where it has no words.</summary>
    public static string TermLabel(string term) => TermLabels.GetValueOrDefault(term, term);

    /// <summary>One measured value of a term, as a percentage where the term is a share.</summary>
    public static string TermValue(string term, double value) =>
        Shares.Contains(term) ? $"{Math.Round(value * 100)}%"
        : Math.Abs(value) >= 10 ? Math.Round(value).ToString(System.Globalization.CultureInfo.InvariantCulture)
        : Math.Round(value, 2).ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>A term's usual range, "a to b"; empty where it has none.</summary>
    public static string BandText(TermDto term) =>
        term.Band is [var low, var high] ? $"{TermValue(term.Term, low)} to {TermValue(term.Term, high)}" : "";

    /// <summary>Where a term's usual range comes from.</summary>
    public static string BandSource(TermDto term) =>
        term.BandSource == "authored" ? "set by the author" : "learned from maps";

    /// <summary>A subject as an author says it.</summary>
    public static string ConcernLabel(RuleConcern concern) => concern switch
    {
        RuleConcern.Intent => "game settings",
        RuleConcern.Feature => "decoration",
        _ => concern.ToString().ToLowerInvariant(),
    };

    /// <summary>Whether a rule is a layout rule — a claim about how a map plays, carrying how it is known —
    /// rather than a check the code raises.</summary>
    public static bool IsLayoutRule(RuleDto rule) => rule.Evidence is not null;

    /// <summary>The word a list shows beside a rule: its action, or what kind of rule it is when it has none.</summary>
    public static string KindWord(RuleDto rule) =>
        IsLayoutRule(rule) ? "Layout rule" : rule.Category is { } category ? ActionOf(category).Label : "Derivation";

    /// <summary>How a layout rule is known, in words; null for a rule that is not one.</summary>
    public static string? Backing(RuleDto rule) => rule.Evidence switch
    {
        RuleEvidence.Author => "The author’s ruling",
        RuleEvidence.Corpus => "Measured on community maps",
        RuleEvidence.Expert => "Expert ruling",
        RuleEvidence.Open => "Open question",
        RuleEvidence.Guess => "Best guess",
        _ => null,
    };

    /// <summary>A rule's text without its markdown, as plain words.</summary>
    public static string Plain(string text) =>
        Emphasis().Replace(text.Replace("**", "").Replace("`", ""), "$1$2");

    /// <summary>The first sentence of a rule's text, for a row that is one line.</summary>
    public static string FirstSentence(string text)
    {
        var plain = Plain(text);
        return Sentence().Match(plain) is { Success: true } first ? first.Groups[1].Value : plain;
    }

    /// <summary>A rule's text as HTML: bold, italics and code marked, everything else escaped.</summary>
    public static string Inline(string text)
    {
        var html = WebUtility.HtmlEncode(text);
        html = Bold().Replace(html, "<b>$1</b>");
        html = Emphasis().Replace(html, "$1<i>$2</i>");
        return Code().Replace(html, "<code>$1</code>");
    }

    [GeneratedRegex(@"^(.{12,}?[.!?])(\s|$)", RegexOptions.Singleline)] private static partial Regex Sentence();
    [GeneratedRegex(@"\*\*([^*]+)\*\*")] private static partial Regex Bold();
    [GeneratedRegex(@"(^|[\s(])\*([^*\s][^*]*?)\*(?=[\s.,;:)]|$)")] private static partial Regex Emphasis();
    [GeneratedRegex(@"`([^`]+)`")] private static partial Regex Code();
}
