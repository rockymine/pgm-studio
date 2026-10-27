using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;
using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>What a picked piece states beside where it stands — a goal's name and structure, a wool's colour, a
/// spawn's facing, how a control point is laid and taken — read from the intent by the piece's id and written back one setting at a time.</summary>
public partial class SketchTool
{
    private ObjectiveVocabularyDto? objectiveWords;
    private ObjectiveFieldValues? pieceSettings;
    private double? spawnYaw;

    /// <summary>The stamps the picked piece has switched off; null for a piece with no stamp to switch.</summary>
    private string[]? stampsOff;

    /// <summary>Read the picked piece's settings and which of its stamps are off, or forget them where the piece
    /// states none.</summary>
    private async Task LoadPieceSettingsAsync()
    {
        pieceSettings = null;
        spawnYaw = null;
        stampsOff = null;
        if (selectedStructural is not { } piece) return;
        var settled = piece.Role is PlayPieceKinds.Spawn or PlayPieceKinds.WoolRoom or PlayPieceKinds.Destroyable
            or PlayPieceKinds.Core or PlayPieceKinds.ControlPoint;
        if (!settled && StampSwitches.Of(piece.Role).Length == 0) return;
        try
        {
            objectiveWords ??= await Http.GetFromJsonAsync<ObjectiveVocabularyDto>("api/objectives/vocabulary");
            var intent = await Http.GetFromJsonAsync<JsonElement>($"api/map/{Slug}/intent");
            var (holder, list) = piece.Role switch
            {
                PlayPieceKinds.Spawn => ("", "spawns"),
                PlayPieceKinds.WoolRoom => ("", "wools"),
                PlayPieceKinds.Destroyable => ("", "destroyables"),
                PlayPieceKinds.ControlPoint => ("", "controlPoints"),
                PlayPieceKinds.BuildArea => ("build", "areas"),
                PlayPieceKinds.Wall => ("structures", "walls"),
                _ => ("", "cores"),
            };
            // A pick made while this read was out is the one the inspector shows.
            if (selectedStructural?.Id != piece.Id) return;
            var parent = intent;
            if (holder.Length > 0 && !intent.TryGetProperty(holder, out parent)) return;
            if (parent.ValueKind != JsonValueKind.Object || !parent.TryGetProperty(list, out var entries)
                || entries.ValueKind != JsonValueKind.Array) return;
            foreach (var entry in entries.EnumerateArray())
            {
                if (!entry.TryGetProperty("id", out var id) || id.GetString() != piece.Id) continue;
                if (StampSwitches.Of(piece.Role).Length > 0)
                    stampsOff = entry.TryGetProperty("stampsOff", out var off) && off.ValueKind == JsonValueKind.Array
                        ? [.. off.EnumerateArray().Select(word => word.GetString() ?? "")]
                        : [];
                if (!settled) continue;
                if (piece.Role == PlayPieceKinds.Spawn)
                    spawnYaw = entry.TryGetProperty("yaw", out var yaw) ? yaw.GetDouble() : 0;
                else
                    pieceSettings = new ObjectiveFieldValues(
                        Text(entry, "name"), Text(entry, "color"), Text(entry, "style"), Text(entry, "materials"),
                        Number(entry, "float"), Number(entry, "lava"), Number(entry, "lavaHeight"),
                        entry.TryGetProperty("openTop", out var open) && open.ValueKind == JsonValueKind.True,
                        Number(entry, "leak"), Text(entry, "shape"), Number(entry, "points"),
                        Seconds(Text(entry, "captureTime")), Text(entry, "captureMode"));
            }
        }
        catch (HttpRequestException) { }
        StateHasChanged();
    }

    /// <summary>Switch one of the picked piece's stamps on or off.</summary>
    private Task SwitchStamp(string word, bool on) => SetPieceSettingAsync(word, on);

    private static string? Text(JsonElement entry, string key) =>
        entry.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
        && value.GetString() is { Length: > 0 } text ? text : null;

    private static int? Number(JsonElement entry, string key) =>
        entry.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number
            ? (int)Math.Round(value.GetDouble()) : null;

    /// <summary>A PGM duration in whole seconds, as the rail states one, or null where it is not one.</summary>
    private static int? Seconds(string? duration) =>
        duration is { Length: > 1 } && duration.EndsWith('s')
        && int.TryParse(duration[..^1], System.Globalization.CultureInfo.InvariantCulture, out var seconds) ? seconds : null;

    /// <summary>Write one setting of the picked piece, and read the board back where it changed a footprint.</summary>
    private async Task SetPieceSettingAsync(string key, object? value)
    {
        if (selectedStructural is not { } piece) return;
        // A setting is a step of the board's undo like a move is, so the bridge holds the intent it replaces.
        if (handle is not null) await handle.InvokeVoidAsync("beginPieceStep");
        HttpResponseMessage response;
        try
        {
            response = await Http.PatchAsJsonAsync($"api/map/{Slug}/intent/pieces/{Uri.EscapeDataString(piece.Id)}/settings",
                new PieceSettingRequest(key, value as string, value as int?, value as bool?));
        }
        finally
        {
            if (handle is not null) await handle.InvokeVoidAsync("endPieceStep");
        }
        if (!response.IsSuccessStatusCode)
        {
            structuralNote = await ServerRefusal.SentenceAsync(response);
            StateHasChanged();
            return;
        }
        structuralNote = null;
        if (handle is not null) await handle.InvokeVoidAsync("refreshPieces");
        await LoadPieceSettingsAsync();
    }

    private Task SetObjectiveField(ObjectiveFieldChange change) => SetPieceSettingAsync(change.Key, change.Value);

    private Task FaceSpawn() => SetPieceSettingAsync(PieceSettings.Facing, 1);
}
