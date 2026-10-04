using System.Net.Http.Json;
using PgmStudio.Client.Components;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>
/// The map's notes as the sketch tool holds them: read in one place for every phase that shows them — Review
/// writes and answers them, History names the threads written in the span it shows. Nothing is read for a
/// caller who may not read notes, and <see cref="Allowed"/> says so.
/// </summary>
public sealed class SketchNotes(HttpClient http, StudioAccess access, string slug)
{
    /// <summary>Raised when a read lands, so whoever draws the notes draws them again.</summary>
    public event Action? Changed;

    /// <summary>Whether the caller may read and answer notes; false until the first read has asked.</summary>
    public bool Allowed { get; private set; }

    /// <summary>The caller's own uuid, which marks their messages as theirs.</summary>
    public string? MeUuid { get; private set; }

    public IReadOnlyList<MapNoteDto> All { get; private set; } = [];

    /// <summary>The agent hand-off the open notes are waiting on, or null.</summary>
    public NoteHandoffDto? Handoff { get; set; }

    /// <summary>Why the last read failed, or null.</summary>
    public string? LoadError { get; private set; }

    /// <summary>Read the notes and the hand-off again.</summary>
    public async Task LoadAsync()
    {
        var me = await access.MeAsync();
        (Allowed, MeUuid) = (me.Notes, me.Uuid);
        if (!Allowed) return;
        try
        {
            All = await http.GetFromJsonAsync<List<MapNoteDto>>($"api/map/{slug}/notes") ?? [];
            Handoff = await http.GetFromJsonAsync<NoteHandoffDto>("api/notes/handoff");
            LoadError = null;
        }
        catch { LoadError = "Couldn't load notes. Check your connection and reload."; }
        Changed?.Invoke();
    }
}
