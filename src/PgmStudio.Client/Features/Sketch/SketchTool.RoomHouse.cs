using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;
using PgmStudio.Client.Features.Library;
using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>A room's house in Layout: read from the intent when one of its wings is picked, and written whole
/// through the bridge, which asks the piece route, draws the rooms again and picks the wing it names.</summary>
public partial class SketchTool
{
    [Inject] private TerrainLibraryClient Library { get; set; } = default!;

    /// <summary>The picked room's house, <c>{wings, style?}</c>; null unless a room's wing is picked in Layout.</summary>
    private JsonObject? roomHouse;

    /// <summary>The room the house is of, and which of its wings is picked, counted from 0.</summary>
    private string? houseRoom;
    private int houseWing;

    private IReadOnlyList<RoomStyleSummary> houseShells = [];

    private static readonly Regex WingId = new(@"-building(?:-(\d+))?$", RegexOptions.CultureInvariant);

    /// <summary>Read the picked wing's house, or forget it where no room's wing is picked in Layout.</summary>
    private async Task LoadRoomHouseAsync()
    {
        roomHouse = null;
        if (!LayoutActive || selectedStructural is not { Role: StructuralRoles.Building } piece) return;
        var found = WingId.Match(piece.Id);
        if (!found.Success) return;
        var room = piece.Id[..found.Index];
        var wing = found.Groups[1].Success ? int.Parse(found.Groups[1].Value) - 1 : 0;
        try
        {
            if (houseShells.Count == 0) houseShells = await Library.ListAsync<RoomStyleSummary>(LibraryKinds.Houses);
            var intent = await Http.GetFromJsonAsync<JsonObject>($"api/map/{Slug}/intent");
            // A pick made while this read was out is the one the inspector shows.
            if (selectedStructural?.Id != piece.Id) return;
            var entry = new[] { "spawns", "wools" }
                .SelectMany(list => intent?[list] as JsonArray ?? [])
                .OfType<JsonObject>()
                .FirstOrDefault(entry => entry["id"]?.GetValue<string>() == room);
            if (entry is null) return;
            houseRoom = room;
            houseWing = wing;
            roomHouse = entry["house"]?.DeepClone() as JsonObject ?? new JsonObject
            {
                ["wings"] = new JsonArray(new JsonObject
                {
                    ["corners"] = new JsonArray(new JsonArray(piece.MinX, piece.MinZ), new JsonArray(piece.MaxX - 1, piece.MaxZ - 1)),
                }),
            };
        }
        catch (HttpRequestException) { }
        StateHasChanged();
    }

    /// <summary>Let the canvas shape a picked room's house and drag a picked wall's ends in Layout alone, and read
    /// the house for the step.</summary>
    private async Task PushLayoutEditingAsync()
    {
        if (handle is null) return;
        await handle.InvokeVoidAsync("setLayoutEditing", LayoutActive);
        await LoadRoomHouseAsync();
    }

    /// <summary>Write the house whole and pick wing <paramref name="pick"/> of it.</summary>
    private Task WriteRoomHouse(JsonObject house, int pick) =>
        handle is null || houseRoom is null
            ? Task.CompletedTask
            : handle.InvokeVoidAsync("setRoomHouse", houseRoom, house.ToJsonString(), pick).AsTask();

    private Task PickRoomWing(int wing) =>
        handle is null || houseRoom is null
            ? Task.CompletedTask
            : handle.InvokeVoidAsync("pickStructural", wing == 0 ? $"{houseRoom}-building" : $"{houseRoom}-building-{wing + 1}").AsTask();

    private Task DeleteRoomWing(int wing) =>
        handle is null || houseRoom is null
            ? Task.CompletedTask
            : handle.InvokeVoidAsync("deleteRoomWing", houseRoom, wing).AsTask();

    private Task SetRoomWingSpec((int Wing, JsonObject Spec) change)
    {
        if (roomHouse?.DeepClone() is not JsonObject house || house["wings"] is not JsonArray wings
            || wings.ElementAtOrDefault(change.Wing) is not JsonObject wing) return Task.CompletedTask;
        if (change.Spec.Count == 0) wing.Remove("spec");
        else wing["spec"] = change.Spec.DeepClone();
        return WriteRoomHouse(house, change.Wing);
    }

    /// <summary>Dress the house in a style: <see cref="SketchRoomHouse.Choice"/> says which.</summary>
    private Task SetRoomHouseStyle(SketchRoomHouse.Choice choice)
    {
        if (roomHouse?.DeepClone() is not JsonObject house) return Task.CompletedTask;
        house.Remove("style");
        if (choice.OpenGround) house["style"] = null;
        else if (choice.Shell is { } shell) house["style"] = JsonNode.Parse(shell.Style);
        return WriteRoomHouse(house, houseWing);
    }
}
