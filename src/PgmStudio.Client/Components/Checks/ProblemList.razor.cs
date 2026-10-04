using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Components;

public partial class ProblemList
{
    [Inject] private RuleBook Book { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>The findings to list, as the caller classed them.</summary>
    [Parameter, EditorRequired] public IReadOnlyList<Problem> Problems { get; set; } = [];

    /// <summary>Above the groups: a heading and the verdict line.</summary>
    [Parameter] public RenderFragment? Head { get; set; }

    /// <summary>Below the groups.</summary>
    [Parameter] public RenderFragment? Foot { get; set; }

    /// <summary>Show one kind only.</summary>
    [Parameter] public ProblemKind? Only { get; set; }

    /// <summary>Leave out the group labels, for a list with one group the place already names.</summary>
    [Parameter] public bool Bare { get; set; }

    /// <summary>Open the first row when the list first has one.</summary>
    [Parameter] public bool OpenFirst { get; set; }

    /// <summary>Buttons a row carries, by rule id: the move that settles that rule.</summary>
    [Parameter] public IReadOnlyDictionary<string, RenderFragment>? Actions { get; set; }

    /// <summary>The findings to light: the pressed place, else every place of every open row; none restores
    /// the caller's own view.</summary>
    [Parameter] public EventCallback<IReadOnlyList<Problem>> OnLight { get; set; }

    private static readonly ProblemKind[] Order = [ProblemKind.Problem, ProblemKind.OutOfRange, ProblemKind.LeftOut, ProblemKind.Warning];

    private IReadOnlyDictionary<string, RuleDto> rules = new Dictionary<string, RuleDto>();
    private IReadOnlyList<TermDto> terms = [];
    private IReadOnlyList<Problem>? shown;
    private List<Row> rows = [];
    private readonly HashSet<string> open = [];
    private Problem? pressed;
    private bool relight;

    /// <summary>One rule's row: its findings of one kind, and for a term out of range, its reading.</summary>
    private sealed record Row(string Key, ProblemKind Kind, string Rule, string? Term, List<Problem> Places, Reading? Reading);

    /// <summary>A term's value against its band, as a sentence and as a bar.</summary>
    private sealed record Reading(double Value, string Sentence, Bar? Bar);

    /// <summary>Positions on a bar from zero, in percent: the band's start and width and the value's mark.</summary>
    private sealed record Bar(double Left, double Width, double Mark);

    protected override async Task OnInitializedAsync()
    {
        rules = await Book.RulesAsync();
        terms = await Book.TermsAsync();
        rows = RowsOf(Problems);
    }

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(shown, Problems)) return;
        var hadAny = shown is { Count: > 0 };
        shown = Problems;
        rows = RowsOf(Problems);
        pressed = null;
        open.IntersectWith(rows.Select(row => row.Key));
        if (OpenFirst && !hadAny && open.Count == 0
            && Order.Where(kind => Only is null || Only == kind)
                .Select(kind => rows.FirstOrDefault(row => row.Kind == kind))
                .FirstOrDefault(row => row is not null) is { } first)
            open.Add(first.Key);
        relight = open.Count > 0;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await JS.InvokeVoidAsync("studio.icons");
        if (!relight) return;
        relight = false;
        await LightAsync();
    }

    private List<Row> RowsOf(IReadOnlyList<Problem> problems)
    {
        var grouped = new List<Row>();
        foreach (var problem in problems)
        {
            var key = $"{problem.Kind}|{problem.Finding.Rule}|{problem.Term}";
            if (grouped.FirstOrDefault(row => row.Key == key) is { } row) row.Places.Add(problem);
            else grouped.Add(new Row(key, problem.Kind, problem.Finding.Rule, problem.Term, [problem], ReadingOf(problem)));
        }
        return grouped;
    }

    private Reading? ReadingOf(Problem problem)
    {
        if (problem is not { Term: { } term, Value: { } value, Band: [var low, var high] }) return null;
        var source = terms.FirstOrDefault(each => each.Term == term) is { } known ? RuleWords.BandSource(known) : "learned from maps";
        var sentence = $"Measured {RuleWords.TermValue(term, value)}. Usually {RuleWords.TermValue(term, low)} to "
                       + $"{RuleWords.TermValue(term, high)}, {source}.";
        var max = Math.Max(high * 1.6, value * 1.15);
        var bar = low < 0 || max <= 0 ? null
            : new Bar(low / max * 100, Math.Max(1.5, (high - low) / max * 100), Math.Min(100, value / max * 100));
        return new Reading(value, sentence, bar);
    }

    private RuleDto? RuleOf(string rule) => rules.GetValueOrDefault(rule);

    /// <summary>What a row says on its one line: the term measured, else the rule's first sentence, else the
    /// first finding's own message.</summary>
    private static string Title(Row row, RuleDto? rule) =>
        row.Term is not null && row.Reading is not null ? RuleWords.TermLabel(row.Term)
        : rule is not null ? RuleWords.FirstSentence(rule.Means)
        : row.Places[0].Finding.Message;

    private async Task ToggleAsync(Row row)
    {
        if (!open.Remove(row.Key)) open.Add(row.Key);
        else if (pressed is not null && row.Places.Contains(pressed)) pressed = null;
        await LightAsync();
    }

    private async Task PressAsync(Problem place)
    {
        pressed = pressed == place ? null : place;
        await LightAsync();
    }

    private Task LightAsync()
    {
        IReadOnlyList<Problem> lit = pressed is not null
            ? [pressed]
            : rows.Where(row => open.Contains(row.Key)).SelectMany(row => row.Places).ToList();
        return OnLight.InvokeAsync(lit);
    }

    private static string Group(ProblemKind kind) => kind switch
    {
        ProblemKind.Problem => "Problems",
        ProblemKind.OutOfRange => "Out of range",
        ProblemKind.LeftOut => "Left out",
        _ => "Warnings",
    };

    private static string One(ProblemKind kind) => kind switch
    {
        ProblemKind.Problem => "Problem",
        ProblemKind.OutOfRange => "Out of range",
        ProblemKind.LeftOut => "Left out",
        _ => "Warning",
    };

    private static string Slug(ProblemKind kind) => kind switch
    {
        ProblemKind.Problem => "problem",
        ProblemKind.OutOfRange => "range",
        ProblemKind.LeftOut => "left",
        _ => "warning",
    };

    private static string Glyph(ProblemKind kind) => kind switch
    {
        ProblemKind.Problem => "circle-x",
        ProblemKind.OutOfRange => "gauge",
        ProblemKind.LeftOut => "triangle-alert",
        _ => "circle-alert",
    };

    private static string PlacesText(int count) => count == 1 ? "1 place" : $"{count} places";

    private static string Pct(double value) => value.ToString("0.##", CultureInfo.InvariantCulture) + "%";
}
