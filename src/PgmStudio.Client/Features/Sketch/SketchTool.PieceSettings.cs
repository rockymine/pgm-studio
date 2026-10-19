using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;
using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>What a picked piece states beside where it stands — a goal's name and structure, a wool's colour, a
/// spawn's facing — read from the intent by the piece's id and written back one setting at a time.</summary>
public partial class SketchTool
{
    private ObjectiveVocabularyDto? objectiveWords;
    private ObjectiveFieldValues? pieceSettings;
    private double? spawnYaw;

    /// <summary>Read the picked piece's settings, or forget them where the piece states none.</summary>
    private async Task LoadPieceSettingsAsync()
    {
        pieceSettings = null;
        spawnYaw = null;
        if (selectedStructural is not { } piece
            || piece.Role is not (PlayPieceKinds.Spawn or PlayPieceKinds.WoolRoom or PlayPieceKinds.Destroyable or PlayPieceKinds.Core))
            return;
        try
        {
            objectiveWords ??= await Http.GetFromJsonAsync<ObjectiveVocabularyDto>("api/objectives/vocabulary");
            var intent = await Http.GetFromJsonAsync<JsonElement>($"api/map/{Slug}/intent");
            var list = piece.Role switch
            {
                PlayPieceKinds.Spawn => "spawns",
                PlayPieceKinds.WoolRoom => "wools",
                PlayPieceKinds.Destroyable => "destroyables",
                _ => "cores",
            };
            // A pick made while this read was out is the one the inspector shows.
            if (selectedStructural?.Id != piece.Id) return;
            if (!intent.TryGetProperty(list, out var entries) || entries.ValueKind != JsonValueKind.Array) return;
            foreach (var entry in entries.EnumerateArray())
            {
                if (!entry.TryGetProperty("id", out var id) || id.GetString() != piece.Id) continue;
                if (piece.Role == PlayPieceKinds.Spawn)
                    spawnYaw = entry.TryGetProperty("yaw", out var yaw) ? yaw.GetDouble() : 0;
                else
                    pieceSettings = new ObjectiveFieldValues(
                        Text(entry, "name"), Text(entry, "color"), Text(entry, "style"), Text(entry, "materials"),
                        Number(entry, "float"), Number(entry, "lava"), Number(entry, "lavaHeight"),
                        entry.TryGetProperty("openTop", out var open) && open.ValueKind == JsonValueKind.True,
                        Number(entry, "leak"));
            }
        }
        catch (HttpRequestException) { }
        StateHasChanged();
    }

    private static string? Text(JsonElement entry, string key) =>
        entry.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
        && value.GetString() is { Length: > 0 } text ? text : null;

    private static int? Number(JsonElement entry, string key) =>
        entry.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : null;

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
