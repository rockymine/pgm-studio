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
        [new(StudioRoles.Member, "Member"), new(StudioRoles.Admin, "Admin")];

    private static readonly IReadOnlyList<SelectOption> MemberOnly = [new(StudioRoles.Member, "Member")];

    private bool? admin;
    private CallerDto? me;
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
        me = await Access.MeAsync();
        if (admin == true) await LoadAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");

    private async Task LoadAsync() =>
        users = await Http.GetFromJsonAsync<List<StudioUserDto>>("api/users") ?? [];

    private bool Owner => me?.Owner == true;

    /// <summary>The roles this caller may give: an owner gives either, an admin only member.</summary>
    private IReadOnlyList<SelectOption> RolesToGive => Owner ? Roles : MemberOnly;

    // Why each control is closed to this caller, or null where it is open. The server refuses the same changes
    // (docs/access.md, "Who keeps the whitelist"); this only says so before the click.
    private string? Untouchable(StudioUserDto user) =>
        user.Owner && !(Owner && me?.Uuid == user.Uuid) ? "Owners are set in the server configuration and can only be changed there"
        : !Owner && user.Role == StudioRoles.Admin ? "Only an owner can change an admin"
        : null;

    private string? RoleLocked(StudioUserDto user) =>
        user.Owner ? "Owners are always admins"
        : Untouchable(user) ?? (Owner ? null : "Only an owner can make someone an admin");

    private string? InviteLocked(StudioUserDto user) =>
        Untouchable(user)
        ?? (!Owner && user.SignsIn ? $"{user.Name} can already sign in. Only an owner can send them a new invitation" : null);

    private string? RemoveLocked(StudioUserDto user) =>
        user.Owner ? "Owners are set in the server configuration and can only be changed there" : Untouchable(user);

    private static string SignInState(StudioUserDto user) =>
        user.SignsIn ? "signs in with Discord"
        : user.InviteExpiresAt is not null ? "invited"
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
        if (!await JS.InvokeAsync<bool>("confirm", $"Remove {user.Name} from the whitelist? They stay credited on their maps but can no longer edit anything."))
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
