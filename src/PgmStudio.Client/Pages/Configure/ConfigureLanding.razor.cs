using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace PgmStudio.Client.Pages.Configure;

public partial class ConfigureLanding
{
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    // Scaffold candidates: the xml-less world folders the Source step would scan. B8 wires the real listing.
    private static readonly (string Slug, string Label, string Regions)[] Candidates =
    [
        ("thunder_blank",         "thunder (blank)",         "12 region files"),
        ("annealing_iv_blank",    "annealing_iv (blank)",    "9 region files"),
        ("outback_edition_blank", "outback_edition (blank)", "15 region files"),
    ];

    private static readonly string[] Steps = ["Source", "Found", "Plan"];
    private IReadOnlyList<string> SubSteps => Steps;

    private int step;
    private string selected = Candidates[0].Slug;

    private bool BackEnabled => step > 0;
    private bool OnPlan => step == Steps.Length - 1;
    private string NextLabel => OnPlan ? "Start authoring" : "Next";

    private void Select(string slug) => selected = slug;
    private void JumpStep(int j) { if (j >= 0 && j < Steps.Length) step = j; }
    private void Back() { if (step > 0) step--; }

    private void Next()
    {
        if (!OnPlan) { step++; return; }
        Nav.NavigateTo($"maps/{selected}/configure");   // Start authoring → Map Info
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
        => await JS.InvokeVoidAsync("studio.icons");
}
