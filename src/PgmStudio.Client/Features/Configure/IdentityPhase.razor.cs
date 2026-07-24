using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;
using PgmStudio.Client.Components;

namespace PgmStudio.Client.Features.Configure;

// Identity phase body: edits the intent's meta slice (name + authors/contributors). The author rows +
// their username resolution are delegated to the shared AuthorsEditor; only verified usernames (resolved
// to a uuid) are written to the intent, so a bad name is caught at the source and never reaches the
// generated map. Edits patch the cascaded wizard's working Intent and mark it dirty; the wizard persists
// meta when the phase is left.
public partial class IdentityPhase
{
    [CascadingParameter] public ConfigureTool Wizard { get; set; } = default!;

    private string name = "";
    private readonly List<AuthorRow> authors = new();
    private readonly List<AuthorRow> contributors = new();

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
    }

    private static void Load(List<AuthorRow> list, JsonObject? meta, string key)
    {
        if (meta?[key] is not JsonArray a) return;
        foreach (var n in a)
        {
            if (n is not JsonObject o) continue;
            var name = o["name"]?.GetValue<string>() ?? "";
            if (name.Length == 0) continue;
            list.Add(new AuthorRow { Name = name, Contribution = o["contribution"]?.GetValue<string>() ?? "" });
        }
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
    private static JsonArray Confirmed(IEnumerable<AuthorRow> people) =>
        new(people.Where(p => p.Uuid.Length > 0 && !p.Error && p.Name.Trim().Length > 0)
                  .Select(p => (JsonNode)new JsonObject
                  {
                      ["name"] = p.Name.Trim(),
                      ["contribution"] = string.IsNullOrWhiteSpace(p.Contribution) ? null : p.Contribution.Trim(),
                  }).ToArray());

    private void OnName(ChangeEventArgs e) { name = e.Value?.ToString() ?? ""; Sync(); }
}
