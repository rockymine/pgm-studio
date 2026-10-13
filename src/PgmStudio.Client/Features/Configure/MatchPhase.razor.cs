using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;
using PgmStudio.Client.Components;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Features.Configure;

using Ctx = AuthoringContext;

// Match phase body: edits the intent's match members — scoreLimit, timeLimit, respawn, lives, rage and killHeight.
// A member left at the board's own default is removed rather than written, so the generator's default applies
// (docs/pgm/match.md §7). Edits patch the cascaded wizard's working Intent and mark it dirty.
public partial class MatchPhase
{
    [CascadingParameter] public ConfigureTool Wizard { get; set; } = default!;

    private int? scoreLimit;
    private string duration = "";
    private string result = TimeResults.Default;
    private int? lives;
    private bool rage;
    private string delay = "";
    private bool auto;
    private bool spectate;
    private int? killHeight;

    private const string Yes = "yes";
    private const string No = "no";

    private static readonly IReadOnlyList<SelectOption> ResultOptions =
    [
        new(TimeResults.Default, "Whoever is ahead", "The team furthest along its objectives, then the higher score."),
        new(TimeResults.Objectives, "Objectives", "The team furthest along its objectives."),
        new(TimeResults.Score, "Score", "The team with the higher score."),
        new(TimeResults.Tie, "A tie", "Nobody wins."),
    ];

    private static readonly IReadOnlyList<SelectOption> YesNo = [new(No, "No"), new(Yes, "Yes")];
    private static readonly IReadOnlyList<SelectOption> AutoOptions = [new(No, "On a click"), new(Yes, "On their own")];
    private static readonly IReadOnlyList<SelectOption> SpectateOptions = [new(No, "They wait"), new(Yes, "They watch the match")];

    protected override void OnInitialized()
    {
        var intent = Wizard.Intent;
        scoreLimit = intent["scoreLimit"] is null ? null : Ctx.I(intent, "scoreLimit", 0);
        if (intent["timeLimit"] is JsonObject time)
        {
            duration = Ctx.S(time, "duration");
            result = Ctx.S(time, "result") is { Length: > 0 } stated ? stated : TimeResults.Default;
        }
        lives = intent["lives"] is null ? null : Ctx.I(intent, "lives", 0);
        rage = intent["rage"]?.GetValue<bool>() ?? false;
        if (intent["respawn"] is JsonObject respawn)
        {
            delay = Ctx.S(respawn, "delay");
            auto = respawn["auto"]?.GetValue<bool>() ?? false;
            spectate = respawn["spectate"]?.GetValue<bool>() ?? false;
        }
        killHeight = intent["killHeight"] is null ? null : Ctx.I(intent, "killHeight", 0);
    }

    private void Sync()
    {
        var intent = Wizard.Intent;
        Set("scoreLimit", scoreLimit);
        intent["timeLimit"] = duration.Length > 0 ? new JsonObject { ["duration"] = duration, ["result"] = result } : null;
        Set("lives", lives is > 0 ? lives : null);
        intent["rage"] = rage ? true : null;
        intent["respawn"] = delay.Length > 0 || auto || spectate
            ? new JsonObject { ["delay"] = delay, ["auto"] = auto, ["spectate"] = spectate }
            : null;
        Set("killHeight", killHeight);
        Wizard.MarkDirty();

        void Set(string key, int? value)
        {
            if (value is { } stated) intent[key] = stated;
            else intent.Remove(key);
        }
    }
}
