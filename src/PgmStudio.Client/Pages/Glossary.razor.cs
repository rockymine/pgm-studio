using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Pages;

public partial class Glossary : IDisposable
{
    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>The filter the address carries: <c>/glossary?q=wool</c>.</summary>
    [SupplyParameterFromQuery(Name = "q")] public string? Query { get; set; }

    private IReadOnlyList<GlossaryTerm>? terms;
    private HashSet<string> slugs = [];
    private string? scrolledTo;

    protected override void OnInitialized() => Nav.LocationChanged += OnLocationChanged;

    protected override async Task OnParametersSetAsync()
    {
        if (terms is not null) return;
        try
        {
            var all = await Http.GetFromJsonAsync<List<GlossaryTerm>>("api/glossary") ?? [];
            terms = [.. all.OrderBy(term => term.Term, StringComparer.OrdinalIgnoreCase)];
            slugs = [.. terms.Select(term => Slug(term.Term))];
        }
        catch (HttpRequestException)
        {
            terms = [];
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await JS.InvokeVoidAsync("studio.icons");
        if (terms is not { Count: > 0 } || Target is not { } target || target == scrolledTo) return;
        // A link to a word the search would hide clears the search, so a link always lands on its entry.
        if (Searching && slugs.Contains(target) && !Ranked.Any(entry => entry.Term is { } term && Slug(term.Term) == target))
        {
            Nav.NavigateTo($"glossary#{target}", replace: true);
            return;
        }
        scrolledTo = target;
        await JS.InvokeVoidAsync("studio.revealId", target);
    }

    /// <summary>The anchor the address names: <c>#hub</c> is the entry whose id is <c>hub</c>.</summary>
    private string? Target
    {
        get
        {
            var at = Nav.Uri.IndexOf('#');
            return at < 0 || at == Nav.Uri.Length - 1 ? null : Uri.UnescapeDataString(Nav.Uri[(at + 1)..]);
        }
    }

    /// <summary>A place in the list: a term, or a retired name filed where a reader would look for it, pointing at
    /// the term that replaced it.</summary>
    private sealed record Filing(string Name, GlossaryTerm? Term, GlossaryTerm? See);

    private static readonly string[] Letters = [.. Enumerable.Range('A', 26).Select(letter => ((char)letter).ToString()), "#"];

    private bool Searching => !string.IsNullOrWhiteSpace(Query);

    /// <summary>Every term and every retired name, A to Z, under its letter.</summary>
    private IReadOnlyDictionary<string, List<Filing>> Filed
    {
        get
        {
            var all = terms ?? [];
            var filings = all.Select(term => new Filing(term.Term, term, null))
                .Concat(all.SelectMany(term => term.AlsoCalled.Select(name => new Filing(name, null, term))));
            return filings.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .GroupBy(entry => Initial(entry.Name))
                .OrderBy(group => group.Key == "#" ? 1 : 0).ThenBy(group => group.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToList());
        }
    }

    /// <summary>What a search finds, best first: a term whose name holds it, a retired name that holds it, then a
    /// term whose definition or other names hold it.</summary>
    private List<Filing> Ranked
    {
        get
        {
            var all = terms ?? [];
            var needle = Query?.Trim() ?? "";
            var named = all.Where(term => Holds(term.Term, needle))
                .OrderBy(term => term.Term.StartsWith(needle, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(term => term.Term, StringComparer.OrdinalIgnoreCase)
                .Select(term => new Filing(term.Term, term, null));
            var retired = all.SelectMany(term => term.AlsoCalled.Where(name => Holds(name, needle))
                    .Select(name => new Filing(name, null, term)))
                .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase);
            var meant = all.Where(term => !Holds(term.Term, needle)
                    && (Holds(term.Definition, needle) || term.AlsoCalled.Any(name => Holds(name, needle))))
                .OrderBy(term => term.Term, StringComparer.OrdinalIgnoreCase)
                .Select(term => new Filing(term.Term, term, null));
            return [.. named, .. retired, .. meant];
        }
    }

    private string CountText
    {
        get
        {
            var total = terms?.Count ?? 0;
            if (!Searching) return $"{total} words";
            var found = Ranked.Count(entry => entry.Term is not null);
            return $"{found} of {total} words";
        }
    }

    private static bool Holds(string text, string needle) => text.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private bool Known(string term) => slugs.Contains(Slug(term));

    private static string Initial(string term) =>
        char.IsLetter(term[0]) ? char.ToUpperInvariant(term[0]).ToString() : "#";

    private static string Slug(string term) => PgmStudio.Vocabulary.Glossary.Slug(term);

    /// <summary>Put the filter in the address, which the page reads it from.</summary>
    private void Go(string? text) =>
        Nav.NavigateTo(Nav.GetUriWithQueryParameter("q", string.IsNullOrWhiteSpace(text) ? null : text), replace: true);

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e) => InvokeAsync(StateHasChanged);

    public void Dispose() => Nav.LocationChanged -= OnLocationChanged;
}
