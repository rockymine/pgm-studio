using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;
using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Pages;

public partial class Users
{
    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private StudioAccess Access { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private static readonly IReadOnlyList<SelectOption> Roles =
        [new(StudioRoles.Member, "member"), new(StudioRoles.Admin, "admin")];

    private bool? admin;
    private List<StudioUserDto>? users;
    private readonly Dictionary<string, InviteDto> invites = new(StringComparer.OrdinalIgnoreCase);
    private string newPlayer = "";
    private string newRole = StudioRoles.Member;
    private bool adding;
    private string? error;
    private string? copied;

    protected override async Task OnInitializedAsync()
    {
        admin = await Access.IsAdminAsync();
        if (admin == true) await LoadAsync();
    }

    private async Task LoadAsync() =>
        users = await Http.GetFromJsonAsync<List<StudioUserDto>>("api/users") ?? [];

    private static string SignInState(StudioUserDto user) =>
        user.SignsIn ? "signs in with Discord"
        : user.InviteExpiresAt is not null ? "invited, not signed in yet"
        : "no invitation";

    private async Task AddAsync()
    {
        adding = true;
        error = null;
        await PutAsync(newPlayer.Trim(), newRole);
        if (error is null) newPlayer = "";
        adding = false;
    }

    private Task SetRoleAsync(StudioUserDto user, string role) => PutAsync(user.Uuid, role);

    private async Task PutAsync(string player, string role)
    {
        using var response = await Http.PostAsJsonAsync("api/users", new StudioUserRequest(player, role));
        if (!response.IsSuccessStatusCode)
        {
            error = await ServerRefusal.SentenceAsync(response);
            return;
        }
        Access.Forget();
        await LoadAsync();
    }

    private async Task InviteAsync(StudioUserDto user)
    {
        error = null;
        using var response = await Http.PostAsync($"api/users/{user.Uuid}/invite", null);
        if (!response.IsSuccessStatusCode)
        {
            error = await ServerRefusal.SentenceAsync(response);
            return;
        }
        invites[user.Uuid] = (await response.Content.ReadFromJsonAsync<InviteDto>())!;
        await LoadAsync();
    }

    private async Task RemoveAsync(StudioUserDto user)
    {
        if (!await JS.InvokeAsync<bool>("confirm", $"Take {user.Name} off the whitelist? They keep their credits and can write nothing more."))
            return;
        error = null;
        using var response = await Http.DeleteAsync($"api/users/{user.Uuid}");
        if (!response.IsSuccessStatusCode)
        {
            error = await ServerRefusal.SentenceAsync(response);
            return;
        }
        invites.Remove(user.Uuid);
        await LoadAsync();
    }

    private async Task CopyAsync(string link)
    {
        await JS.InvokeVoidAsync("navigator.clipboard.writeText", link);
        copied = link;
    }
}
