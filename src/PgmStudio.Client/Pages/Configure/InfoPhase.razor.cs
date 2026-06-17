using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;

namespace PgmStudio.Client.Pages.Configure;

// Map Info phase body: edits the intent's meta slice (name + authors/contributors). Reads the working
// intent from the cascaded wizard on entry and writes every edit straight back to it (marking dirty),
// so the wizard persists meta when the phase is left. Author usernames are resolved to UUIDs server-side.
public partial class InfoPhase
{
    [CascadingParameter] public ConfigureWizard Wizard { get; set; } = default!;

    private string name = "";
    private List<string> authors = new();
    private List<string> contributors = new();

    // Auto-derived identity shown locked — the generator (MetaGenerator) sets these, not the author.
    private const string Version = "1.0.0";
    private const string Mode = "ctw";
    private const string Objective = "Capture the other teams' wools and bring them to your monuments to win.";

    protected override void OnInitialized()
    {
        var meta = Wizard.Intent["meta"] as JsonObject;
        name = meta?["name"]?.GetValue<string>() ?? "";
        authors = ReadList(meta, "authors");
        contributors = ReadList(meta, "contributors");
        if (authors.Count == 0) authors.Add("");   // always show one author row to fill in
    }

    private static List<string> ReadList(JsonObject? o, string key) =>
        o?[key] is JsonArray a ? a.Select(n => n?.GetValue<string>() ?? "").ToList() : new();

    // Project the edited identity back onto the wizard's working intent and flag it dirty; blank names
    // are dropped so the stored lists hold only real usernames.
    private void Sync()
    {
        Wizard.Intent["meta"] = new JsonObject
        {
            ["name"] = name,
            ["authors"] = ToArray(authors),
            ["contributors"] = ToArray(contributors),
        };
        Wizard.MarkDirty();
    }

    private static JsonArray ToArray(IEnumerable<string> items) =>
        new(items.Select(s => s.Trim()).Where(s => s.Length > 0).Select(s => (JsonNode)JsonValue.Create(s)!).ToArray());

    private void AddAuthor() { authors.Add(""); Sync(); }
    private void RemoveAuthor(int i) { authors.RemoveAt(i); if (authors.Count == 0) authors.Add(""); Sync(); }

    private void AddContributor() { contributors.Add(""); Sync(); }
    private void RemoveContributor(int i) { contributors.RemoveAt(i); Sync(); }
}
