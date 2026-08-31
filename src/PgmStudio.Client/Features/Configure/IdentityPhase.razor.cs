using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;
using PgmStudio.Client.Components;

using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Features.Configure;

// Identity phase body: edits the intent's meta slice (name + authors/contributors). The author rows and the
// account lookup behind them are delegated to the shared AuthorsEditor; what is written to the intent is
// every row that names somebody, since PGM takes an author as an account or as a pseudonym. Edits patch the
// cascaded wizard's working Intent and mark it dirty; the wizard persists meta when the phase is left.
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

    // Every row that names somebody reaches the intent. PGM takes a person as an account or as a pseudonym
    // and either alone is a whole author, so a name no Minecraft account carries is the second kind rather
    // than a failed lookup — the API resolves the uuid where there is one and keeps the stated name where
    // there is not. What is dropped is a row that is not a name at all (AuthorNames.IsWritable), because
    // there is nobody in it to credit.
    private static JsonArray Confirmed(IEnumerable<AuthorRow> people) =>
        new(people.Where(p => !p.Error && AuthorNames.IsWritable(p.Name.Trim()))
                  .Select(p => (JsonNode)new JsonObject
                  {
                      ["name"] = p.Name.Trim(),
                      ["contribution"] = string.IsNullOrWhiteSpace(p.Contribution) ? null : p.Contribution.Trim(),
                  }).ToArray());

    private void OnName(ChangeEventArgs e) { name = e.Value?.ToString() ?? ""; Sync(); }
}
