using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;

namespace PgmStudio.Client.Components;

/// <summary>One author/contributor row: the canonical <c>Uuid</c> is the persisted identity, <c>Name</c>
/// the resolved (or typed) username, <c>Contribution</c> an optional note; <c>Error</c> flags a username
/// that didn't resolve to a Minecraft account.</summary>
public sealed class AuthorRow
{
    public string Uuid = "";
    public string Name = "";
    public string Contribution = "";
    public bool Error;
}

public partial class AuthorsEditor
{
    [Parameter, EditorRequired] public List<AuthorRow> Authors { get; set; } = new();
    [Parameter] public List<AuthorRow>? Contributors { get; set; }
    /// <summary>Raised on every edit (add/remove/type/resolve) so the parent can mark itself dirty.</summary>
    [Parameter] public EventCallback OnChanged { get; set; }

    private const string AvatarEmpty = "data:image/gif;base64,R0lGODlhEAAQAAAAACwAAAAAEAAQAAABEIQBADs=";

    // Rows whose stored uuid we've already kicked a display-name lookup for (by reference), so a parent
    // re-render doesn't re-fetch. Keyed on the row object, not the uuid, so an edited row re-resolves.
    private readonly HashSet<AuthorRow> nameResolved = new();

    private IEnumerable<AuthorRow> All() => Contributors is null ? Authors : Authors.Concat(Contributors);

    protected override void OnParametersSet()
    {
        // Fill display names for rows loaded with a stored uuid but no cached name (best-effort).
        foreach (var p in All().Where(p => p.Uuid.Length > 0 && p.Name.Length == 0 && nameResolved.Add(p)))
            _ = ResolveByUuid(p);
    }

    private void Add(List<AuthorRow> list) { list.Add(new AuthorRow()); NotifyChanged(); }
    private void Remove(List<AuthorRow> list, AuthorRow p) { list.Remove(p); NotifyChanged(); }

    private void NotifyChanged() => _ = OnChanged.InvokeAsync();

    /// <summary>Resolve a stored uuid to its current username for display (does not raise OnChanged).</summary>
    private async Task ResolveByUuid(AuthorRow p)
    {
        try
        {
            var r = await Http.GetFromJsonAsync<JsonElement>($"api/minecraft/player?uuid={Uri.EscapeDataString(p.Uuid)}");
            p.Name = Str(r, "name");
            StateHasChanged();
        }
        catch { /* leave the uuid showing if Mojang is unreachable / renamed-away */ }
    }

    /// <summary>On blur: look the typed value up via Mojang, storing the canonical uuid (the persisted
    /// identity) and resolved name. Clears the uuid + flags an error on a miss.</summary>
    private async Task ResolveName(AuthorRow p)
    {
        var val = p.Name.Trim();
        if (val.Length == 0) { p.Uuid = ""; p.Error = false; return; }
        var isUuid = val.Contains('-') && val.Length > 30;
        var q = isUuid ? $"uuid={Uri.EscapeDataString(val)}" : $"name={Uri.EscapeDataString(val)}";
        try
        {
            var r = await Http.GetFromJsonAsync<JsonElement>($"api/minecraft/player?{q}");
            p.Uuid = Str(r, "uuid"); p.Name = Str(r, "name"); p.Error = false;
        }
        catch { p.Uuid = ""; p.Error = true; }
        NotifyChanged();
        StateHasChanged();
    }

    private static string Str(JsonElement e, string key)
        => e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}
