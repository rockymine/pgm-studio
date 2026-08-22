using System.Text.Json.Nodes;
using PgmStudio.Data.Schema;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Vocabulary;
using PgmStudio.Domain;

namespace PgmStudio.Api.Services;

/// <summary>What confirming a symmetry came to: a refusal, or nothing to say.</summary>
public sealed record SymmetryConfirmed(Refusal? Refusal);

/// <summary>
/// What an author says about a map's symmetry, over what the detector guessed.
///
/// <para>Detection answers a ranked guess and the author answers whether it is right, so this writes the
/// half of the row that is theirs: the status, the type they confirmed, and the centre the folds are taken
/// about. A field the request leaves out is one they did not answer, and keeps whatever the detector or a
/// previous confirmation put there — which is why the payload is read key by key rather than bound.</para>
///
/// <para><b>The two are stored together and stay distinguishable.</b> A confirmed type sets
/// <c>PrimaryUserOverride</c>, so a later detection pass cannot quietly overwrite an answer; saying there is
/// no symmetry clears the guess along with the override, because the author has answered that too.</para>
/// </summary>
public static class SymmetryConfirm
{
    public static async Task<SymmetryConfirmed> StateAsync(
        PgmDb db, long mapId, string body, CancellationToken ct)
    {
        var stated = (JsonNode.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body) as JsonObject) ?? [];
        var row = await SymmetryStore.LoadAsync(db, mapId, ct)
            ?? new SymmetryRow { MapId = mapId, Status = "unconfirmed", ModesJson = "[]" };

        var status = stated["status"]?.GetValue<string>();
        if (status is "confirmed" or "none") row.Status = status;

        if (stated["confirmed_type"] is { } confirmed)
        {
            var type = confirmed.GetValue<string>();
            if (!SymmetrySupport.ValidTypes.Contains(type))
                return new(Refusal.At(400, "invalid symmetry type",
                    new Finding(RequestRules.Unreadable,
                        $"'{type}' is not a symmetry type the studio knows", Field: "confirmed_type")));

            row.PrimaryType = type; row.PrimaryConfidence = 1.0; row.PrimaryUserOverride = true;
        }
        else if (status == "none")
        {
            row.PrimaryType = null; row.PrimaryConfidence = null; row.PrimaryUserOverride = false;
        }

        if (stated.ContainsKey("cx") || stated.ContainsKey("cz"))
        {
            row.CenterX = stated["cx"]?.GetValue<double>() ?? row.CenterX ?? 0.0;
            row.CenterZ = stated["cz"]?.GetValue<double>() ?? row.CenterZ ?? 0.0;
        }

        await SymmetryStore.SaveAsync(db, row, ct);
        return new((Refusal?)null);
    }
}
