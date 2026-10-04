using System.Net.Http.Json;
using System.Text.Json;
using PgmStudio.Client.Components;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>
/// The Review phase's views: the gallery the studio answers, the camera in hand while one is placed, and the
/// calls that keep, change and let go of them. The gallery, the list beside the canvas and the inspector are
/// all drawn from it; what the canvas shows of them is the host's, handed in as delegates.
/// </summary>
/// <param name="setViews">Draws the kept and suggested cameras, from the JSON the bridge's <c>setViews</c> takes.</param>
/// <param name="setDraft">Draws the camera in hand, or takes it away with null.</param>
/// <param name="setBoardView">Shows the board layer the cameras are placed on, or hides it.</param>
public sealed class SketchViews(
    HttpClient http, string slug, Func<string, Task> setViews, Func<string?, Task> setDraft, Func<bool, Task> setBoardView)
{
    /// <summary>Raised whenever anything the phase shows changes.</summary>
    public event Action? Changed;

    /// <summary>The gallery, or null while it is read.</summary>
    public MapViewsDto? Gallery { get; private set; }
    public string? Error { get; private set; }

    /// <summary>Why the last thing asked of a view — letting it go, drawing the map's picture from it — was
    /// refused, said over the gallery rather than in place of it.</summary>
    public string? Refusal { get; private set; }

    /// <summary>Moves on at every entry and every redraw, so a picture of a board that has changed since is
    /// asked for again.</summary>
    public int Round { get; private set; }

    public void NextRound() => Round++;

    /// <summary>The camera in hand while one is placed, or null.</summary>
    public SketchViewDraft.ViewDraft? Draft { get; private set; }

    /// <summary>Why the camera in hand could not be drawn or kept, or null.</summary>
    public string? Note { get; private set; }

    /// <summary>Whether the canvas is being used to place a view of the author's own.</summary>
    public bool Placing { get; private set; }

    public IReadOnlyList<MapViewDto> Kept => Gallery?.Views.Where(view => view.Kept).ToList() ?? [];
    public IReadOnlyList<MapViewDto> Suggested => Gallery?.Views.Where(view => !view.Kept).ToList() ?? [];

    /// <summary>Read the views again. Clearing first shows the phase reading; a redraw keeps the gallery up, so a
    /// note half written beside it stays.</summary>
    public async Task LoadAsync(bool clear = true)
    {
        Error = null;
        if (clear)
        {
            Gallery = null;
            Changed?.Invoke();
        }
        try { Gallery = await http.GetFromJsonAsync<MapViewsDto>($"api/map/{slug}/views"); }
        catch { Error = "Couldn't load views. Check your connection and try again."; }
        await setViews(JsonSerializer.Serialize((Gallery?.Views ?? []).Select(view => new
        {
            id = view.Id, kept = view.Kept, fromX = view.FromX, fromZ = view.FromZ, lookX = view.LookX, lookZ = view.LookZ,
            eyeX = view.Eye?.X, eyeZ = view.Eye?.Z,
        })));
        Changed?.Invoke();
    }

    /// <summary>Leave the phase: nothing is being placed and the canvas shows no camera.</summary>
    public async Task ClearCanvasAsync()
    {
        Placing = false;
        Draft = null;
        await setViews("[]");
        await setDraft(null);
        await setBoardView(false);
    }

    /// <summary>A view picked up to change or to copy: stood where it stands — where its eye resolved to, for
    /// one that leaves the eye to find its own place — at the height and tip it states or resolved to.</summary>
    private static SketchViewDraft.ViewDraft DraftOf(MapViewDto view, int? fromX, int? fromZ, int lookX, int lookZ)
    {
        var resolved = view.FromX is null ? view.Eye : null;
        return new SketchViewDraft.ViewDraft(
            fromX ?? view.FromX ?? (view.Eye is { } eye ? (int)Math.Floor(eye.X) : null),
            fromZ ?? view.FromZ ?? (view.Eye is { } seen ? (int)Math.Floor(seen.Z) : null),
            lookX, lookZ,
            view.Y ?? resolved?.Y, view.Pitch ?? resolved?.Pitch, view, view.Yaw);
    }

    /// <summary>The inspector resolved the camera in hand: the canvas draws it where it stands and where its
    /// middle lands.</summary>
    public Task AimedAsync((int FromX, int FromZ, int LookX, int LookZ) aim) =>
        setDraft(JsonSerializer.Serialize(new
        {
            id = Draft?.Source?.Id, fromX = aim.FromX, fromZ = aim.FromZ, lookX = aim.LookX, lookZ = aim.LookZ,
        }));

    /// <summary>Pick a view up from the list beside the canvas, as a press on its camera would.</summary>
    public async Task SelectAsync(MapViewDto view)
    {
        Draft = DraftOf(view, null, null, view.LookX, view.LookZ);
        Note = null;
        await setDraft(JsonSerializer.Serialize(new
        {
            id = view.Id, fromX = Draft.FromX, fromZ = Draft.FromZ, lookX = view.LookX, lookZ = view.LookZ,
        }));
        Changed?.Invoke();
    }

    /// <summary>The canvas's eye tool was released: a stand point and what it looks at, or only the latter —
    /// for a new view, or for the camera <paramref name="id"/> names, picked up and moved.</summary>
    public void Picked(int? fromX, int? fromZ, int lookX, int lookZ, string? id)
    {
        Draft = Gallery?.Views.FirstOrDefault(view => view.Id == id) is { } picked
            ? DraftOf(picked, fromX, fromZ, lookX, lookZ)
            : new SketchViewDraft.ViewDraft(fromX, fromZ, lookX, lookZ);
        Note = null;
        Changed?.Invoke();
    }

    /// <summary>The board could not be built for the Board layer: the canvas stays empty under the views, and
    /// the inspector says why.</summary>
    public void BoardUnavailable(string reason)
    {
        Note = reason is { Length: > 0 } ? $"Couldn't draw the map: {reason}" : "Couldn't draw the map.";
        Changed?.Invoke();
    }

    /// <summary>Start placing a view of one's own: the board layer under an empty hand.</summary>
    public async Task BeginPlacingAsync()
    {
        Placing = true;
        Draft = null;
        Note = null;
        await setBoardView(true);
        Changed?.Invoke();
    }

    /// <summary>Leave placing for the gallery.</summary>
    public async Task EndPlacingAsync()
    {
        Placing = false;
        Draft = null;
        await setDraft(null);
        await setBoardView(false);
    }

    /// <summary>Put the camera in hand down without keeping it.</summary>
    public async Task DiscardAsync()
    {
        Draft = null;
        Note = null;
        await setDraft(null);
        Changed?.Invoke();
    }

    /// <summary>Store the camera in hand: a kept view picked up is changed in place, and anything else — a new
    /// view, or a suggestion picked up — is kept as a new one. True where it landed.</summary>
    public async Task<bool> KeepAsync(MapViewKeepRequest request)
    {
        try
        {
            var answer = Draft?.Source is { Kept: true } changed
                ? await http.PutAsJsonAsync($"api/map/{slug}/views/{Uri.EscapeDataString(changed.Id)}", request)
                : await http.PostAsJsonAsync($"api/map/{slug}/views", request);
            if (!answer.IsSuccessStatusCode)
            {
                var refusal = await answer.Content.ReadFromJsonAsync<RefusalDto>();
                Note = refusal?.Message is { Length: > 0 } why ? why : "Couldn't save the view.";
                return false;
            }
        }
        catch
        {
            Note = "Couldn't save the view. Check your connection and try again.";
            return false;
        }
        return true;
    }

    public async Task LetGoAsync(MapViewDto view)
    {
        try
        {
            using var answer = await http.DeleteAsync($"api/map/{slug}/views/{Uri.EscapeDataString(view.Id)}");
            if (!answer.IsSuccessStatusCode)
            {
                Refusal = $"Couldn't remove the view: {await ServerRefusal.SentenceAsync(answer)}";
                return;
            }
        }
        catch
        {
            Refusal = "Couldn't remove the view. Check your connection and try again.";
            return;
        }
        Refusal = null;
        await LoadAsync(clear: false);
    }

    /// <summary>Draw the map's picture from a view: a kept one is marked in place, and a suggestion is kept as
    /// a view of its own, marked.</summary>
    public async Task PictureAsync(MapViewDto view)
    {
        var request = new MapViewKeepRequest(view.Name, view.LookX, view.LookZ, view.FromX, view.FromZ, view.Y,
                                             view.Pitch, view.Yaw, Picture: true);
        try
        {
            var answer = view.Kept
                ? await http.PutAsJsonAsync($"api/map/{slug}/views/{Uri.EscapeDataString(view.Id)}", request)
                : await http.PostAsJsonAsync($"api/map/{slug}/views", request);
            if (!answer.IsSuccessStatusCode)
            {
                Refusal = $"Couldn't set the map picture: {await ServerRefusal.SentenceAsync(answer)}";
                return;
            }
        }
        catch
        {
            Refusal = "Couldn't set the map picture. Check your connection and try again.";
            return;
        }
        Refusal = null;
        await LoadAsync(clear: false);
    }
}
