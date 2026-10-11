using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Pages;

public partial class Join
{
    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>Where the sign-in was headed, a path on this studio.</summary>
    [SupplyParameterFromQuery] public string? ReturnUrl { get; set; }

    /// <summary>How many maps a person waiting for approval may own, as the page states it.</summary>
    private const int Limit = 3;

    private string? discordName;
    private bool lapsed;
    private string player = "";
    private PlayerDto? found;
    private bool joining;
    private string? error;

    protected override async Task OnInitializedAsync()
    {
        using var response = await Http.GetAsync("api/auth/join");
        if (response.IsSuccessStatusCode)
            discordName = (await response.Content.ReadFromJsonAsync<JoinStateDto>())!.DiscordName;
        else lapsed = true;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");

    /// <summary>Show whose account the name is, so the person confirms it before joining as it.</summary>
    private async Task LookUpAsync()
    {
        error = null;
        found = null;
        if (string.IsNullOrWhiteSpace(player)) return;
        using var response = await Http.GetAsync($"api/minecraft/player?name={Uri.EscapeDataString(player.Trim())}");
        if (response.IsSuccessStatusCode) found = await response.Content.ReadFromJsonAsync<PlayerDto>();
        else error = $"No Minecraft account is called '{player.Trim()}'.";
    }

    private async Task JoinAsync()
    {
        if (found is null) return;
        joining = true;
        error = null;
        using var response = await Http.PostAsJsonAsync("api/auth/join", new JoinRequest(found.Uuid));
        if (response.IsSuccessStatusCode)
        {
            var home = ReturnUrl is { Length: > 1 } path && path.StartsWith('/') && !path.StartsWith("//") ? path : "/";
            Nav.NavigateTo(home, forceLoad: true);
            return;
        }
        if (response.StatusCode == HttpStatusCode.Unauthorized) lapsed = true;
        else error = await ServerRefusal.SentenceAsync(response);
        joining = false;
    }
}
