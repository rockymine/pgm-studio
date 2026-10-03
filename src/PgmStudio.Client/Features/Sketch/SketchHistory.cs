using System.Net.Http.Json;
using System.Text.Json;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>
/// The History phase's state: the board's changes, the span drawn on the canvas with what it did, and putting
/// the board back. The list and the inspector are both drawn from it; the canvas and the tool's saving are the
/// host's, handed in as delegates.
/// </summary>
/// <param name="drawDiff">Draws the span on the canvas, from the JSON the bridge's <c>setDiff</c> takes.</param>
/// <param name="saveFirst">Stores what the canvas holds before a restore and answers the sentence it was
/// refused with, or null when it landed.</param>
/// <param name="reloadBoard">Takes up the board a restore wrote as the canvas's own.</param>
public sealed class SketchHistory(
    HttpClient http, string slug, Func<string, Task> drawDiff, Func<Task<string?>> saveFirst, Func<Task> reloadBoard)
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    /// <summary>Raised whenever anything the phase shows changes.</summary>
    public event Action? Changed;

    public MapChangesDto? Changes { get; private set; }
    public string? ChangesError { get; private set; }

    /// <summary>The span drawn on the canvas: from one change to another, the one before it for a single
    /// change. <see cref="SpanTo"/> is null until one is picked.</summary>
    public long SpanFrom { get; private set; }
    public long? SpanTo { get; private set; }
    public MapDiffDto? SpanDiff { get; private set; }
    public WorldChangesDto? SpanWorld { get; private set; }
    public string? SpanWorldError { get; private set; }

    public bool Restoring { get; private set; }
    public string? RestoreError { get; private set; }

    /// <summary>The layouts at either end of the span, kept so coming back to History draws it again.</summary>
    private JsonElement? spanBefore, spanAfter;

    /// <summary>Bumped on every pick, so an answer to an earlier one arriving late is dropped.</summary>
    private int spanRound;

    public IReadOnlyList<long> ChangeNumbers => Changes?.Changes.Select(change => change.Number).ToList() ?? [];
    public long LatestChange => Changes?.Changes.LastOrDefault()?.Number ?? 0;
    public MapChangeDto? SpanChange => Changes?.Changes.FirstOrDefault(change => change.Number == SpanTo);

    /// <summary>The threads with a message written in the span shown, each with its latest such message.</summary>
    public IReadOnlyList<(MapNoteDto Note, NoteMessageDto Message)> NotesInSpan(IEnumerable<MapNoteDto> notes) =>
        SpanTo is not { } to ? []
        : [.. notes
            .Select(note => (Note: note, Message: note.Messages.LastOrDefault(message => message.Change > SpanFrom && message.Change <= to)))
            .Where(entry => entry.Message is not null)
            .Select(entry => (entry.Note, entry.Message!))];

    public async Task LoadChangesAsync()
    {
        ChangesError = null;
        try { Changes = await http.GetFromJsonAsync<MapChangesDto>($"api/map/{slug}/changes"); }
        catch { ChangesError = "Couldn't load the change history. Check your connection and try again."; }
        Changed?.Invoke();
    }

    /// <summary>Open the phase: the span already shown is drawn again, and otherwise the latest change is
    /// picked.</summary>
    public async Task OpenAsync()
    {
        if (SpanTo is null) { if (LatestChange > 0) await PickChange(LatestChange); }
        else await DrawSpan(spanBefore, spanAfter, SpanWorld);
    }

    /// <summary>Draw what one change did: from the change before it to it.</summary>
    public Task PickChange(long number) =>
        ShowSpan(Changes?.Changes.LastOrDefault(change => change.Number < number)?.Number ?? 0, number);

    /// <summary>Draw a span: the documents first, which are quick, and the columns when both builds are done.</summary>
    public async Task ShowSpan(long from, long to)
    {
        var round = ++spanRound;
        (SpanFrom, SpanTo) = (from, to);
        SpanDiff = null;
        SpanWorld = null;
        SpanWorldError = null;
        RestoreError = null;
        Changed?.Invoke();

        MapDiffDto? diff = null;
        JsonElement? before = null, after = null;
        try
        {
            diff = await http.GetFromJsonAsync<MapDiffDto>($"api/map/{slug}/diff?from={from}&to={to}");
            before = from == 0 ? null : (await http.GetFromJsonAsync<MapChangeDocumentsDto>($"api/map/{slug}/changes/{from}"))?.Layout;
            after = (await http.GetFromJsonAsync<MapChangeDocumentsDto>($"api/map/{slug}/changes/{to}"))?.Layout;
        }
        catch { SpanWorldError = "Couldn't load this change. Check your connection and try again."; }
        if (round != spanRound) return;
        SpanDiff = diff;
        (spanBefore, spanAfter) = (before, after);
        await DrawSpan(before, after, null);
        Changed?.Invoke();

        if (before is null || after is null)
        {
            SpanWorldError ??= "There's nothing before the first change to build.";
            Changed?.Invoke();
            return;
        }
        try
        {
            var built = await http.GetFromJsonAsync<MapDiffDto>($"api/map/{slug}/diff?from={from}&to={to}&world=true");
            if (round != spanRound) return;
            SpanWorld = built?.World;
            await DrawSpan(before, after, SpanWorld);
        }
        catch
        {
            if (round != spanRound) return;
            SpanWorldError = "Couldn't build both versions.";
        }
        Changed?.Invoke();
    }

    private Task DrawSpan(JsonElement? before, JsonElement? after, WorldChangesDto? world) =>
        drawDiff(JsonSerializer.Serialize(new { before, after, world }, Wire));

    /// <summary>Put the board back as it stood at a change: the studio writes it as a new change, and the canvas
    /// takes up the board it wrote.</summary>
    public async Task RestoreAsync(long number)
    {
        if (await saveFirst() is { } refused)
        {
            RestoreError = refused;
            Changed?.Invoke();
            return;
        }
        Restoring = true;
        RestoreError = null;
        Changed?.Invoke();
        try
        {
            using var answer = await http.PostAsJsonAsync($"api/map/{slug}/changes/{number}/restore", new MapRestoreRequest());
            if (!answer.IsSuccessStatusCode)
            {
                var refusal = await answer.Content.ReadFromJsonAsync<RefusalDto>();
                RestoreError = refusal?.Message is { Length: > 0 } why ? $"Couldn't restore. {why}" : $"Couldn't restore (HTTP {(int)answer.StatusCode}). Try again.";
                return;
            }
            await reloadBoard();
            await LoadChangesAsync();
            if (LatestChange > 0) await PickChange(LatestChange);
        }
        catch { RestoreError = "Couldn't restore. Check your connection and try again."; }
        finally
        {
            Restoring = false;
            Changed?.Invoke();
        }
    }
}
