using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Data.Schema;

namespace PgmStudio.Data.Map;

/// <summary>A note and its thread, oldest message first.</summary>
public sealed record StoredNote(MapNoteRow Note, IReadOnlyList<MapNoteMessageRow> Messages);

/// <summary>
/// The notes left on maps and the threads under them. A note names its map by slug, so a board rebuilt under
/// the same slug keeps every thread about it; deleting a map is what lets its notes go.
/// </summary>
public sealed class MapNoteStore(PgmDb db)
{
    /// <summary>One map's notes, newest first.</summary>
    public Task<List<StoredNote>> OfMapAsync(string slug, CancellationToken ct = default) =>
        ThreadsAsync(db.MapNotes.Where(note => note.MapSlug == slug), ct);

    /// <summary>Every note in one of <paramref name="statuses"/> on any map, newest change first; every note
    /// where none is named. <paramref name="since"/> keeps those whose thread moved at or after it, in UTC.</summary>
    public Task<List<StoredNote>> AcrossMapsAsync(IReadOnlyCollection<string> statuses, DateTime? since = null,
                                                  CancellationToken ct = default)
    {
        var asked = statuses.Count == 0 ? db.MapNotes : db.MapNotes.Where(note => statuses.Contains(note.Status));
        return ThreadsAsync(since is { } after ? asked.Where(note => note.UpdatedAt >= after) : asked, ct);
    }

    /// <summary>One note on <paramref name="slug"/>, or null where that map holds none by this id.</summary>
    public async Task<StoredNote?> GetAsync(string slug, long id, CancellationToken ct = default) =>
        (await ThreadsAsync(db.MapNotes.Where(note => note.MapSlug == slug && note.Id == id), ct)).FirstOrDefault();

    /// <summary>Keep a note with its first message, in one write. Answers it as stored.</summary>
    public Task<StoredNote> CreateAsync(MapNoteRow note, MapNoteMessageRow first, CancellationToken ct = default) =>
        db.InOneWriteAsync(async () =>
        {
            note.Id = await db.InsertWithInt64IdentityAsync(note, token: ct);
            first.NoteId = note.Id;
            first.Id = await db.InsertWithInt64IdentityAsync(first, token: ct);
            return new StoredNote(note, [first]);
        }, ct);

    /// <summary>Add a message to a thread and leave it in <paramref name="status"/>, in one write.</summary>
    public Task AddAsync(MapNoteMessageRow message, string status, CancellationToken ct = default) =>
        db.InOneWriteAsync(async () =>
        {
            message.Id = await db.InsertWithInt64IdentityAsync(message, token: ct);
            await db.MapNotes.Where(note => note.Id == message.NoteId)
                .Set(note => note.Status, status)
                .Set(note => note.UpdatedAt, message.CreatedAt)
                .UpdateAsync(ct);
        }, ct);

    /// <summary>Change a note's status.</summary>
    public Task ChangeAsync(long id, string status, DateTime at, CancellationToken ct = default) =>
        db.MapNotes.Where(note => note.Id == id)
            .Set(note => note.Status, status)
            .Set(note => note.UpdatedAt, at)
            .UpdateAsync(ct);

    /// <summary>Let go of every note on a map, with its thread. Answers how many there were.</summary>
    public Task<int> DeleteMapAsync(string slug, CancellationToken ct = default) =>
        db.InOneWriteAsync(async () =>
        {
            var ids = db.MapNotes.Where(note => note.MapSlug == slug).Select(note => note.Id);
            await db.MapNoteMessages.Where(message => ids.Contains(message.NoteId)).DeleteAsync(ct);
            return await db.MapNotes.Where(note => note.MapSlug == slug).DeleteAsync(ct);
        }, ct);

    /// <summary>Every picture a message names, which is what the picture store keeps.</summary>
    public Task<List<string>> PicturesAsync(CancellationToken ct = default) =>
        db.MapNoteMessages.Where(message => message.Picture != null).Select(message => message.Picture!)
            .Distinct().ToListAsync(ct);

    private async Task<List<StoredNote>> ThreadsAsync(IQueryable<MapNoteRow> notes, CancellationToken ct)
    {
        var rows = await notes.OrderByDescending(note => note.UpdatedAt).ThenByDescending(note => note.Id).ToListAsync(ct);
        if (rows.Count == 0) return [];
        var ids = rows.Select(note => note.Id).ToList();
        var messages = (await db.MapNoteMessages.Where(message => ids.Contains(message.NoteId))
                .OrderBy(message => message.Id).ToListAsync(ct))
            .ToLookup(message => message.NoteId);
        return [.. rows.Select(note => new StoredNote(note, [.. messages[note.Id]]))];
    }
}
