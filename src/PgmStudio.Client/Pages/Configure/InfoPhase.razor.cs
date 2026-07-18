using System.Net.Http;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;
using PgmStudio.Client.Models;

namespace PgmStudio.Client.Pages.Configure;

// Map Info phase body: edits the intent's meta slice (name + authors/contributors). Mirrors the Overview
// editor's author handling — a username resolves against Mojang on blur (GET /minecraft/player) to its
// canonical name + uuid (→ mc-heads avatar), or is flagged. Only verified usernames are written to the
// intent, so a bad name is caught at the source and never reaches the generated map. Edits patch the
// cascaded wizard's working Intent and mark it dirty; the wizard persists meta when the phase is left.
public partial class InfoPhase
{
    [CascadingParameter] public ConfigureWizard Wizard { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;

    private sealed class Person { public string Name = ""; public string Uuid = ""; public bool Error; }

    private string name = "";
    private readonly List<Person> authors = new();
    private readonly List<Person> contributors = new();

    // Auto-derived identity shown locked — the generator (MetaGenerator) sets these, not the author.
    private const string Version = "1.0.0";
    private const string Mode = "ctw";
    private const string Objective = "Capture the other teams' wools and bring them to your monuments to win.";

    protected override void OnInitialized()
    {
        var meta = Wizard.Intent["meta"] as JsonObject;
        name = meta?["name"]?.GetValue<string>() ?? "";
        Load(authors, meta, "authors");
        Load(contributors, meta, "contributors");
        if (authors.Count == 0) authors.Add(new Person());
    }

    private static void Load(List<Person> list, JsonObject? meta, string key)
    {
        if (meta?[key] is not JsonArray a) return;
        foreach (var n in a)
        {
            var s = n?.GetValue<string>() ?? "";
            if (s.Length > 0) list.Add(new Person { Name = s });
        }
    }

    // Resolve stored usernames to uuids so their heads show on revisit (best-effort, no dirty flag).
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        foreach (var p in authors.Concat(contributors)
                     .Where(p => p.Name.Length > 0 && p.Uuid.Length == 0 && !p.Error).ToList())
        {
            await Lookup(p);
            StateHasChanged();
        }
    }

    private void OnInput(Person p, ChangeEventArgs e)
    {
        p.Name = e.Value?.ToString() ?? "";
        p.Uuid = ""; p.Error = false;   // editing clears the verified head until blur re-checks
        Sync();
    }

    // On blur: resolve the typed username, then republish the slice (which re-evaluates Next).
    private async Task ResolveName(Person p) { await Lookup(p); Sync(); }

    private async Task Lookup(Person p)
    {
        var val = p.Name.Trim();
        if (val.Length == 0) { p.Uuid = ""; p.Error = false; return; }
        // unknown username (or Mojang unreachable) → flagged, kept out of the intent
        if (await MinecraftPlayer.ResolveAsync(Http, val) is { } r)
        { p.Uuid = r.Uuid; p.Name = r.Name; p.Error = false; }
        else { p.Uuid = ""; p.Error = true; }
    }

    private void Sync()
    {
        Wizard.Intent["meta"] = new JsonObject
        {
            ["name"] = name,
            ["authors"] = Confirmed(authors),
            ["contributors"] = Confirmed(contributors),
        };
        Wizard.MarkDirty();
    }

    // Only verified usernames (resolved to a uuid, no error) reach the intent — an unchecked / unknown
    // name is never persisted, so it can't silently survive into the generated map.
    private static JsonArray Confirmed(IEnumerable<Person> people) =>
        new(people.Where(p => p.Uuid.Length > 0 && !p.Error && p.Name.Trim().Length > 0)
                  .Select(p => (JsonNode)JsonValue.Create(p.Name.Trim())!).ToArray());

    private void OnName(ChangeEventArgs e) { name = e.Value?.ToString() ?? ""; Sync(); }

    private void AddAuthor() => authors.Add(new Person());
    private void AddContributor() => contributors.Add(new Person());

    private void Remove(Person p)
    {
        if (authors.Remove(p)) { if (authors.Count == 0) authors.Add(new Person()); }
        else contributors.Remove(p);
        Sync();
    }
}
