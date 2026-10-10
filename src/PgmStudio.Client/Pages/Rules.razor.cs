using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;
using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Pages;

public partial class Rules
{
    [Inject] private RuleBook Book { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private StudioAccess Access { get; set; } = default!;

    /// <summary>The rule the address opens on: <c>/rules?rule=PL9</c>.</summary>
    [SupplyParameterFromQuery(Name = "rule")] public string? Asked { get; set; }

    private enum Kind { All, Checks, Layout }

    /// <summary>The facet a count leaves out, so a filter's own options count what picking them would show.</summary>
    private enum Facet { None, Kind, Category, Concerns }

    private static readonly (Kind Value, string Label, string Says)[] Kinds =
    [
        (Kind.All, "All", "Every rule"),
        (Kind.Checks, "Checks", "Rules a check can stop a map on"),
        (Kind.Layout, "Layout", "How a good map is laid out. The studio measures plans against these."),
    ];

    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private IReadOnlyDictionary<string, RuleDto>? rules;
    private IReadOnlyList<TermDto> terms = [];
    private ElementReference list;

    private string query = "";
    private Kind kind = Kind.All;
    private RuleCategory? category;
    private readonly HashSet<RuleConcern> concerns = [];
    private string? selected;
    private bool reveal;
    private bool copied;
    private bool admin;
    private bool settingLevel;
    private string? levelError;

    /// <summary>The levels an admin picks between: none of their own (the configuration or the code decides), always
    /// stop, never stop.</summary>
    private static readonly (string? Value, string Label)[] LevelChoices =
    [
        (null, "Default"),
        (RuleLevels.Refuse, "Stops the work"),
        (RuleLevels.Hint, "Hint only"),
    ];

    protected override async Task OnParametersSetAsync()
    {
        if (rules is null)
        {
            rules = await Book.RulesAsync();
            terms = await Book.TermsAsync();
            try { admin = await Access.IsAdminAsync(); } catch (HttpRequestException) { admin = false; }
        }
        if (Asked is not null && Asked != selected && rules.ContainsKey(Asked))
        {
            selected = Asked;
            copied = false;
            reveal = true;
        }
        selected ??= Shown.FirstOrDefault()?.FirstOrDefault()?.Rule;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await JS.InvokeVoidAsync("studio.icons");
        if (!reveal) return;
        reveal = false;
        await JS.InvokeVoidAsync("studio.revealSelected", list);
    }

    private RuleDto? Selected => selected is not null && rules is not null ? rules.GetValueOrDefault(selected) : null;

    /// <summary>The rules the filters let through, by family in the order a map is made.</summary>
    private List<IGrouping<string, RuleDto>> Shown =>
        (rules?.Values ?? []).Where(rule => Passes(rule, Facet.None))
            .GroupBy(rule => rule.Family)
            .OrderBy(family => RuleWords.FamilyRank(family.Key)).ThenBy(family => family.Key, StringComparer.Ordinal)
            .ToList();

    private IEnumerable<RuleConcern> AllConcerns =>
        (rules?.Values ?? []).SelectMany(Concerns).Distinct()
            .OrderBy(RuleWords.ConcernLabel, StringComparer.Ordinal);

    private string CountText
    {
        get
        {
            var total = rules?.Count ?? 0;
            var shown = Count(rule => Passes(rule, Facet.None));
            return shown == total ? $"{total} rules" : $"{shown} of {total} rules";
        }
    }

    private static IReadOnlyList<RuleConcern> Concerns(RuleDto rule) => rule.Concerns ?? [];

    private static bool IsKind(RuleDto rule, Kind value) => value switch
    {
        Kind.Checks => !RuleWords.IsLayoutRule(rule),
        Kind.Layout => RuleWords.IsLayoutRule(rule),
        _ => true,
    };

    private bool Passes(RuleDto rule, Facet ignore)
    {
        if (query.Trim() is { Length: > 0 } words
            && !rule.Rule.Contains(words, StringComparison.OrdinalIgnoreCase)
            && !rule.Means.Contains(words, StringComparison.OrdinalIgnoreCase)) return false;
        if (ignore != Facet.Kind && !IsKind(rule, kind)) return false;
        if (ignore != Facet.Category && category is not null && rule.Category != category) return false;
        if (ignore != Facet.Concerns && !concerns.All(Concerns(rule).Contains)) return false;
        return true;
    }

    private int Count(Func<RuleDto, bool> keep) => (rules?.Values ?? []).Count(keep);

    private List<TermDto> MeasuredAs(RuleDto rule) =>
        terms.Where(term => term.Rule == rule.Rule && term.Band is not null).ToList();

    private void PickKind(Kind value)
    {
        kind = value;
        if (value == Kind.Layout) category = null;
        reveal = true;
    }

    private void PickCategory(RuleCategory? value)
    {
        category = category == value ? null : value;
        if (category is not null && kind == Kind.Layout) kind = Kind.All;
        reveal = true;
    }

    private void ToggleConcern(RuleConcern concern)
    {
        if (!concerns.Remove(concern)) concerns.Add(concern);
        reveal = true;
    }

    private void Reveal() => reveal = true;

    private void Select(string rule)
    {
        selected = rule;
        copied = false;
        Nav.NavigateTo(Nav.GetUriWithQueryParameter("rule", rule), replace: true);
    }

    private async Task CopyAsync(RuleDto rule) =>
        copied = await JS.InvokeAsync<bool>("studio.copyText", JsonSerializer.Serialize(rule, Wire));

    /// <summary>What a finding citing <paramref name="rule"/> does here, and who said so.</summary>
    private static string LevelText(RuleDto rule)
    {
        var by = rule.LevelSource switch
        {
            RuleLevelSources.Mode => "set by the studio's minimal mode",
            RuleLevelSources.Config => "set in the studio's configuration",
            RuleLevelSources.Studio => "set by an admin",
            _ => "",
        };
        return rule.Level switch
        {
            RuleLevels.Refuse => $"Stops the work wherever a check asks, {by}.",
            RuleLevels.Hint => $"Never stops the work: the finding is shown beside the result, {by}.",
            _ => "Does what its code says: where a check refuses on it the work stops, where it remarks the finding rides along.",
        };
    }

    /// <summary>The level an admin has set on <paramref name="rule"/>, or null where the rule has none of its own.</summary>
    private static string? StudioLevel(RuleDto rule) => rule.LevelSource == RuleLevelSources.Studio ? rule.Level : null;

    private async Task SetLevelAsync(RuleDto rule, string? level)
    {
        settingLevel = true;
        levelError = null;
        var updated = await Book.SetLevelAsync(rule.Rule, level);
        settingLevel = false;
        if (updated is null)
        {
            levelError = "The studio did not take the change. Reload the page and try again.";
            return;
        }
        rules = await Book.RulesAsync();
    }
}
