using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Pages;

public partial class Tokens
{
    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private StudioAccess Access { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private CallerDto? me;
    private List<StudioTokenDto>? tokens;
    private StudioTokenIssuedDto? issued;
    private string label = "";
    private bool issuing;
    private bool copied;
    private string? error;

    /// <summary>The address a client sends its requests to, as the mapgen tools name it.</summary>
    private string ApiRoot => Nav.BaseUri.TrimEnd('/') + "/api";

    protected override async Task OnInitializedAsync()
    {
        me = await Access.MeAsync();
        if (me?.Role is not null) await LoadAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");

    private async Task LoadAsync() =>
        tokens = await Http.GetFromJsonAsync<List<StudioTokenDto>>("api/users/me/tokens") ?? [];

    private static string LastUse(StudioTokenDto token) =>
        token.LastUsedAt is { } used ? $"last used {used.ToLocalTime():dd.MM.yyyy HH:mm}" : "never used";

    private async Task IssueAsync()
    {
        issuing = true;
        error = null;
        copied = false;
        using var response = await Http.PostAsJsonAsync("api/users/me/tokens", new StudioTokenRequest(label.Trim()));
        if (response.IsSuccessStatusCode)
        {
            issued = await response.Content.ReadFromJsonAsync<StudioTokenIssuedDto>();
            label = "";
            await LoadAsync();
        }
        else error = await ServerRefusal.SentenceAsync(response);
        issuing = false;
    }

    private async Task RevokeAsync(StudioTokenDto token)
    {
        if (!await JS.InvokeAsync<bool>("confirm", $"Revoke '{token.Label}'? Whatever holds it signs in as nobody from then on."))
            return;
        error = null;
        using var response = await Http.DeleteAsync($"api/users/me/tokens/{token.Id}");
        if (!response.IsSuccessStatusCode)
        {
            error = await ServerRefusal.SentenceAsync(response);
            return;
        }
        if (issued?.Id == token.Id) issued = null;
        await LoadAsync();
    }

    private async Task CopyAsync(string token)
    {
        await JS.InvokeVoidAsync("navigator.clipboard.writeText", token);
        copied = true;
    }
}
