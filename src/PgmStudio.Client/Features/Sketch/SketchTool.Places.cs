using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;
using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>Draw's Plan step: the map's places, drawn over the board with the notes step's tools, listed, picked
/// and named, and stored whole through <c>PUT /map/{slug}/places</c> after every change. Review's Cameras step
/// draws them too.</summary>
public partial class SketchTool
{
    private PlacesDocument placesDocument = new();
    private string? selectedPlaceId;
    private string? placeNote;
    private IReadOnlyList<SelectOption> placePieces = [];

    private bool PlanActive => DrawActive && drawStep == PlanStep;

    private IReadOnlyList<Place> PlaceList => placesDocument.Places ?? [];

    private Place? SelectedPlace => PlaceList.FirstOrDefault(place => place.Id == selectedPlaceId);

    private static string PlaceTool(string shape) => $"place:{shape}";

    /// <summary>Open Draw on its Plan step, where a new map starts.</summary>
    private async Task OpenPlan()
    {
        await GoDraw();
        await StepDraw(PlanStep);
    }

    /// <summary>Read the map's places and the play pieces one may hold.</summary>
    private async Task LoadPlacesAsync()
    {
        try
        {
            placesDocument = await Http.GetFromJsonAsync<PlacesDocument>($"api/map/{Slug}/places") ?? new();
            var pieces = await Http.GetFromJsonAsync<List<PlayPieceDto>>($"api/map/{Slug}/intent/pieces") ?? [];
            placePieces = [.. pieces.Where(piece => !piece.Carried).Select(piece => new SelectOption(piece.Id, piece.Id))];
        }
        catch (HttpRequestException) { }
        await notes.LoadAsync();
        await PushPlacesAsync();
    }

    /// <summary>Draw the places on the canvas while the Plan step or Review's Cameras step is up, and let a click
    /// pick one in Plan.</summary>
    private async Task PushPlacesAsync()
    {
        if (handle is null) return;
        await handle.InvokeVoidAsync("setPlacesPickable", PlanActive);
        await handle.InvokeVoidAsync("setPlaces",
            PlanActive || CamerasActive ? JsonSerializer.Serialize(PlaceList, PlacesJson) : "[]");
        await handle.InvokeVoidAsync("selectPlace", selectedPlaceId);
    }

    private static readonly JsonSerializerOptions PlacesJson = new(JsonSerializerDefaults.Web);

    private async Task ArmPlace(string shape)
    {
        tool = PlaceTool(shape);
        if (handle is not null) await handle.InvokeVoidAsync("armPlace", shape);
    }

    /// <summary>A place drawn on the canvas: stored under the next free id, named after its number, and
    /// picked.</summary>
    [JSInvokable]
    public Task OnPlaceDrawn(string json) => PlaceDrawnAsync(json);

    private async Task PlaceDrawnAsync(string json)
    {
        var drawn = JsonSerializer.Deserialize<JsonElement>(json);
        var shape = drawn.GetProperty("shape").GetString() ?? PlaceShapes.Point;
        var points = drawn.GetProperty("points").Deserialize<double[][]>() ?? [];
        var number = 1;
        while (PlaceList.Any(place => place.Id == $"place-{number}")) number++;
        var place = new Place($"place-{number}", $"Place {number}", shape, points);
        selectedPlaceId = place.Id;
        tool = "select";
        await StorePlacesAsync([.. PlaceList, place]);
    }

    [JSInvokable]
    public void OnPlaceSelected(string? id)
    {
        selectedPlaceId = id;
        placeNote = null;
        StateHasChanged();
    }

    private async Task SelectPlace(string id)
    {
        selectedPlaceId = id;
        placeNote = null;
        if (handle is not null) await handle.InvokeVoidAsync("selectPlace", id);
    }

    private Task ChangePlace(Place changed) =>
        StorePlacesAsync([.. PlaceList.Select(place => place.Id == changed.Id ? changed : place)]);

    private async Task DeletePlace()
    {
        var gone = selectedPlaceId;
        selectedPlaceId = null;
        await StorePlacesAsync([.. PlaceList.Where(place => place.Id != gone)]);
    }

    private async Task ClosePlace()
    {
        selectedPlaceId = null;
        placeNote = null;
        if (handle is not null) await handle.InvokeVoidAsync("selectPlace", (string?)null);
    }

    /// <summary>Say where the places stand with the author.</summary>
    private Task SetVerdict(string verdict) => StoreAsync(placesDocument with { Verdict = verdict });

    private Task StorePlacesAsync(List<Place> places) => StoreAsync(placesDocument with { Places = places });

    /// <summary>Store the places as the whole document, and draw what was stored; a refusal keeps what the map
    /// holds and says why.</summary>
    private async Task StoreAsync(PlacesDocument next)
    {
        var response = await Http.PutAsJsonAsync($"api/map/{Slug}/places", next, PlacesJson);
        if (response.IsSuccessStatusCode)
        {
            placesDocument = next;
            placeNote = null;
        }
        else placeNote = await ServerRefusal.SentenceAsync(response);
        await PushPlacesAsync();
        StateHasChanged();
    }
}

/// <summary>How a place is shown: the glyph for its shape, and its one thread.</summary>
public static class SketchPlaces
{
    /// <summary>The thread pinned to <paramref name="placeId"/>, or null where it has none yet.</summary>
    public static MapNoteDto? Thread(IEnumerable<MapNoteDto> notes, string? placeId) =>
        placeId is null ? null
        : notes.FirstOrDefault(note => note.Anchor.Kind == NoteAnchors.Place && note.Anchor.Place == placeId);

    public static string Icon(string shape) => shape switch
    {
        PlaceShapes.Point => "pin",
        PlaceShapes.Box => "rectangle-horizontal",
        PlaceShapes.Line => "spline",
        _ => "pentagon",
    };
}
