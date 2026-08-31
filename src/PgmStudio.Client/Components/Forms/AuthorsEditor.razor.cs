using PgmStudio.Contracts;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;

using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Components;

/// <summary>One person on a map. PGM takes an author as an account — a <see cref="Uuid"/> it resolves to a
/// player — or as a pseudonym, the element's own text, and either alone is a whole author. So an empty
/// <see cref="Uuid"/> is a state and not a failure: what it says is that no account is called this, and the
/// name stands on its own. <see cref="Error"/> is the other thing entirely — the typed string is not a name
/// anybody could be called, and that row cannot be stored.</summary>
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

    // Rows whose stored uuid we've already kicked a display-name lookup for (by reference), so a parent
    // re-render doesn't re-fetch. Keyed on the row object, not the uuid, so an edited row re-resolves.
    private readonly HashSet<AuthorRow> nameResolved = new();

    private IEnumerable<AuthorRow> All() => Contributors is null ? Authors : Authors.Concat(Contributors);

    protected override void OnParametersSet()
    {
        // Fill the missing half of each loaded row (best-effort, once per row): a stored uuid without a
        // cached name resolves uuid → name; a stored name shaped like an account resolves name → uuid. A
        // pseudonym has no second half to fill, so nothing is asked for it.
        foreach (var p in All().Where(p => !p.Error && p.Name.Length > 0 != p.Uuid.Length > 0 && nameResolved.Add(p)))
        {
            if (p.Uuid.Length > 0) _ = ResolveByUuid(p);
            else if (AuthorNames.IsAccountName(p.Name.Trim())) _ = ResolveByName(p);
        }
    }

    private void Add(List<AuthorRow> list) { list.Add(new AuthorRow()); NotifyChanged(); }
    private void Remove(List<AuthorRow> list, AuthorRow p) { list.Remove(p); NotifyChanged(); }

    private void NotifyChanged() => _ = OnChanged.InvokeAsync();

    /// <summary>Resolve a stored uuid to its current username for display (does not raise OnChanged).</summary>
    private async Task ResolveByUuid(AuthorRow p)
    {
        try
        {
            var player = await Http.GetFromJsonAsync<PlayerDto>($"api/minecraft/player?uuid={Uri.EscapeDataString(p.Uuid)}");
            if (player is { Name.Length: > 0 }) { p.Name = player.Name; StateHasChanged(); }
        }
        catch { /* leave the uuid showing if Mojang is unreachable / renamed-away */ }
    }

    /// <summary>Resolve a stored name to the uuid behind it, for a row persisted name-only (an intent meta
    /// slice keeps no uuid). Best-effort and additive: a name no account answers for is a pseudonym, which
    /// is a whole author, so nothing is cleared and no error is raised.</summary>
    private async Task ResolveByName(AuthorRow p)
    {
        try
        {
            var player = await Http.GetFromJsonAsync<PlayerDto>($"api/minecraft/player?name={Uri.EscapeDataString(p.Name.Trim())}");
            if (player is { Uuid.Length: > 0 }) { p.Uuid = player.Uuid; p.Name = player.Name; StateHasChanged(); }
        }
        catch { /* no account under that name, or no route to ask — the typed name is the author */ }
    }

    /// <summary>Resolve a typed row on blur, and settle which of the three things it is.
    ///
    /// <para>A name nobody could be called is <see cref="AuthorRow.Error"/> and is asked of nothing. A name
    /// shaped like an account is looked up, and where one answers the row takes its canonical uuid and
    /// spelling. Everything else — a name no account carries, and a lookup that could not be made at all —
    /// is a <b>pseudonym</b>: the name stands, the uuid stays empty, and the row is stored. The studio may be
    /// offline and the credits still hold.</para></summary>
    private async Task ResolveName(AuthorRow p)
    {
        var typed = p.Name.Trim();
        if (typed.Length == 0) { p.Uuid = ""; p.Error = false; NotifyChanged(); StateHasChanged(); return; }

        p.Error = !AuthorNames.IsWritable(typed);
        if (p.Error) { p.Uuid = ""; NotifyChanged(); StateHasChanged(); return; }

        p.Uuid = "";
        if (AuthorNames.IsAccountName(typed) || LooksLikeUuid(typed))
        {
            var query = LooksLikeUuid(typed) ? $"uuid={Uri.EscapeDataString(typed)}" : $"name={Uri.EscapeDataString(typed)}";
            try
            {
                var player = await Http.GetFromJsonAsync<PlayerDto>($"api/minecraft/player?{query}");
                if (player is { Uuid.Length: > 0 }) { p.Uuid = player.Uuid; p.Name = player.Name; }
            }
            catch { /* no account, or nothing to ask — either way the typed name is the author */ }
        }
        NotifyChanged();
        StateHasChanged();
    }

    private static bool LooksLikeUuid(string value)
    {
        var bare = value.Replace("-", "");
        return bare.Length == 32 && bare.All(Uri.IsHexDigit);
    }

    /// <summary>The letter standing for a row, or a dash while it is empty.</summary>
    private static string Initial(AuthorRow p)
    {
        var typed = p.Name.Trim();
        return typed.Length == 0 ? "–" : char.ToUpperInvariant(typed[0]).ToString();
    }

    /// <summary>The row's mark, coloured from the identity it carries — the uuid where an account answered,
    /// the typed name otherwise — so two people are told apart at a glance and one person keeps their colour
    /// across every tool that draws this editor. Drawn from what the row already holds rather than fetched:
    /// a head fetched per row is an unreviewed request from the author's own browser, and it is the first
    /// thing to break on a restricted network.</summary>
    private static string MarkStyle(AuthorRow p)
    {
        var identity = p.Uuid.Length > 0 ? p.Uuid : p.Name.Trim().ToLowerInvariant();
        if (identity.Length == 0) return "";

        // FNV-1a over the identity, folded to a hue. Any stable hash does; this one is four lines.
        var hash = 2166136261u;
        foreach (var c in identity) { hash ^= c; hash *= 16777619u; }
        return $"--author-hue: {hash % 360}";
    }
}
