using System.Net.Http.Json;
using System.Text;
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
        scrolledTo = target;
        await JS.InvokeVoidAsync("studio.scrollToId", target);
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

    /// <summary>The terms whose name, other names or definition hold what was typed.</summary>
    private List<GlossaryTerm> Shown
    {
        get
        {
            var all = terms ?? [];
            if (string.IsNullOrWhiteSpace(Query)) return [.. all];
            var needle = Query.Trim();
            return [.. all.Where(term => Holds(term.Term, needle) || Holds(term.Definition, needle)
                                        || term.AlsoCalled.Any(name => Holds(name, needle)))];
        }
    }

    private string CountText =>
        Shown.Count == (terms?.Count ?? 0) ? $"{Shown.Count} words" : $"{Shown.Count} of {terms?.Count ?? 0}";

    private static bool Holds(string text, string needle) => text.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private bool Known(string term) => slugs.Contains(Slug(term));

    private static string Initial(string term) =>
        char.IsLetter(term[0]) ? char.ToUpperInvariant(term[0]).ToString() : "#";

    /// <summary>A term's anchor: lower-case letters and digits, any run of anything else a single hyphen.</summary>
    private static string Slug(string term)
    {
        var slug = new StringBuilder();
        foreach (var letter in term.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(letter)) slug.Append(letter);
            else if (slug.Length > 0 && slug[^1] != '-') slug.Append('-');
        }
        return slug.ToString().TrimEnd('-');
    }

    /// <summary>Put the filter in the address, which the page reads it from.</summary>
    private void Go(string? text) =>
        Nav.NavigateTo(Nav.GetUriWithQueryParameter("q", string.IsNullOrWhiteSpace(text) ? null : text), replace: true);

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e) => InvokeAsync(StateHasChanged);

    public void Dispose() => Nav.LocationChanged -= OnLocationChanged;
}
