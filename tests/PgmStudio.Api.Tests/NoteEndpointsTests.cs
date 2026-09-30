using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Tests;

/// <summary>
/// Notes on a map, in an open studio where every request is the local admin: a note is pinned to what it is
/// about and keeps its picture, its thread moves between waiting on an agent and waiting on the author, and a
/// rebuild of the board under the same slug keeps every thread while deleting the map lets them go.
/// </summary>
[NotInParallel("api-db")]
public sealed class NoteEndpointsTests
{
    private static string Notes => $"/api/map/{SketchBoard.Slug}/notes";

    private static readonly NoteAnchorDto OnAPicture = new(
        NoteAnchors.Point, "above", "Straight down", new EyeCameraDto(0.5, 120, 1.5, 180, 90, 70), 1280, 720,
        [new PixelDto(640, 360)], new BlockAtDto(0, 21, 0), new BlockAtDto(0, 20, 0));

    [Test]
    public async Task A_note_waits_for_an_agent_and_a_reply_hands_it_back_until_the_author_resolves_it()
    {
        using var client = await SketchBoard.FreshAsync();

        var note = await (await client.PostAsJsonAsync(Notes,
            new MapNoteRequest("This tree floats.", OnAPicture, NoteTags.Look))).Content.ReadFromJsonAsync<MapNoteDto>();
        await Assert.That(note!.Status).IsEqualTo(NoteStatuses.Open);
        await Assert.That(note.Anchor.Hit).IsEqualTo(new BlockAtDto(0, 21, 0));
        await Assert.That(note.Anchor.Marks!.Single()).IsEqualTo(new PixelDto(640, 360));
        await Assert.That(note.Messages.Single().Change).IsGreaterThan(0);

        var asked = await (await client.PostAsJsonAsync($"{Notes}/{note.Id}/replies",
            new NoteReplyRequest("The tree, or the boulder beside it?", NoteStatuses.NeedsInfo))).Content.ReadFromJsonAsync<MapNoteDto>();
        await Assert.That(asked!.Status).IsEqualTo(NoteStatuses.NeedsInfo);
        var answered = await (await client.PostAsJsonAsync($"{Notes}/{note.Id}/replies",
            new NoteReplyRequest("The tree."))).Content.ReadFromJsonAsync<MapNoteDto>();
        await Assert.That(answered!.Status).IsEqualTo(NoteStatuses.Open);
        await Assert.That(answered.Messages.Select(message => message.Body))
            .IsEquivalentTo(["This tree floats.", "The tree, or the boulder beside it?", "The tree."]);

        var resolving = await client.PostAsJsonAsync($"{Notes}/{note.Id}/replies",
            new NoteReplyRequest("Done.", NoteStatuses.Resolved));
        await Assert.That(resolving.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var resolved = await (await client.PatchAsJsonAsync($"{Notes}/{note.Id}",
            new NoteChangeRequest(NoteStatuses.Resolved, Tag: ""))).Content.ReadFromJsonAsync<MapNoteDto>();
        await Assert.That(resolved!.Status).IsEqualTo(NoteStatuses.Resolved);
        await Assert.That(resolved.Tag).IsNull();

        await Assert.That(await client.GetFromJsonAsync<List<MapNoteDto>>("/api/notes?status=open")).IsEmpty();
        await Assert.That((await client.GetFromJsonAsync<List<MapNoteDto>>("/api/notes?status=resolved"))!.Single().MapName)
            .IsEqualTo("Dressed");
    }

    [Test]
    public async Task A_note_that_is_not_one_is_refused_naming_the_field()
    {
        using var client = await SketchBoard.FreshAsync();

        foreach (var (request, field) in new (MapNoteRequest, string)[]
                 {
                     (new MapNoteRequest(" ", new NoteAnchorDto(NoteAnchors.Map)), "body"),
                     (new MapNoteRequest("x", new NoteAnchorDto("somewhere")), "anchor.kind"),
                     (new MapNoteRequest("x", OnAPicture with { Camera = null }), "anchor.camera"),
                     (new MapNoteRequest("x", OnAPicture with { Kind = NoteAnchors.Box }), "anchor.marks"),
                     (new MapNoteRequest("x", OnAPicture with { Marks = [new PixelDto(1280, 0)] }), "anchor.marks"),
                     (new MapNoteRequest("x", new NoteAnchorDto(NoteAnchors.Map), "vibes"), "tag"),
                     (new MapNoteRequest("x", new NoteAnchorDto(NoteAnchors.Map), Picture: new string('a', 64)), "picture"),
                 })
        {
            var refused = await client.PostAsJsonAsync(Notes, request);
            await Assert.That(refused.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
            var finding = (await refused.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("findings")[0];
            await Assert.That(finding.GetProperty("field").GetString()).IsEqualTo(field);
        }
        await Assert.That((await client.GetAsync("/api/map/nowhere/notes")).StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That((await client.PostAsJsonAsync($"{Notes}/999/replies", new NoteReplyRequest("x"))).StatusCode)
            .IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task A_picture_is_kept_once_under_its_hash_and_served_back()
    {
        using var client = await SketchBoard.FreshAsync();
        var webp = "RIFF\0\0\0\0WEBPVP8 test-bytes"u8.ToArray();

        var kept = await Keep(client, webp, "image/webp");
        var again = await Keep(client, webp, "image/webp");
        await Assert.That(again.Hash).IsEqualTo(kept.Hash);
        await Assert.That(kept.Hash).Length().IsEqualTo(64);

        var served = await client.GetAsync($"/api/notes/pictures/{kept.Hash}");
        await Assert.That(served.Content.Headers.ContentType!.MediaType).IsEqualTo("image/webp");
        await Assert.That(await served.Content.ReadAsByteArrayAsync()).IsEquivalentTo(webp);

        var note = await (await client.PostAsJsonAsync(Notes,
            new MapNoteRequest("Bare.", OnAPicture with { Kind = NoteAnchors.View, Marks = null }, Picture: kept.Hash)))
            .Content.ReadFromJsonAsync<MapNoteDto>();
        await Assert.That(note!.Messages.Single().Picture).IsEqualTo(kept.Hash);

        var hello = new ByteArrayContent("hello"u8.ToArray());
        hello.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        var notAPicture = await client.PostAsync("/api/notes/pictures", hello);
        await Assert.That(notAPicture.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That((await client.GetAsync($"/api/notes/pictures/{new string('0', 64)}")).StatusCode)
            .IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task A_rebuild_under_the_same_slug_keeps_the_threads_and_counts_the_changes_on()
    {
        using var client = await SketchBoard.FreshAsync();
        var note = await (await client.PostAsJsonAsync(Notes, new MapNoteRequest("Too flat.", new NoteAnchorDto(NoteAnchors.Map))))
            .Content.ReadFromJsonAsync<MapNoteDto>();
        await client.PostAsJsonAsync($"/api/map/{SketchBoard.Slug}/views", new MapViewKeepRequest("Aerial", 0, 0, 0, 10, 90, 60));

        await SketchBoard.RebuildAsync(client);

        var kept = (await client.GetFromJsonAsync<List<MapNoteDto>>(Notes))!.Single();
        await Assert.That(kept.Id).IsEqualTo(note!.Id);
        await Assert.That(await LatestChangeAsync(client)).IsGreaterThan(note.Messages[0].Change)
            .Because("the rebuild landed as a change after the one the note was written at");
        var views = await client.GetFromJsonAsync<MapViewsDto>($"/api/map/{SketchBoard.Slug}/views");
        await Assert.That(views!.Views.Single(view => view.Kept && !view.Own).Pitch).IsEqualTo(60.0);

        await client.DeleteAsync($"/api/map/{SketchBoard.Slug}");
        await SketchBoard.RebuildAsync(client);
        await Assert.That(await client.GetFromJsonAsync<List<MapNoteDto>>(Notes)).IsEmpty();
    }

    /// <summary>A message records the change its board stood at when it was written, which is what a layout edit
    /// moves; a message stating one keeps it, and one naming a change that has not landed is refused.</summary>
    [Test]
    public async Task A_message_records_the_change_the_board_stood_at()
    {
        using var client = await SketchBoard.FreshAsync();
        var loaded = await LatestChangeAsync(client);
        var layout = await client.GetStringAsync($"/api/map/{SketchBoard.Slug}/sketch");
        await client.PutAsync($"/api/map/{SketchBoard.Slug}/sketch",
            new StringContent(layout.Replace("\"max_x\":20", "\"max_x\":24"), System.Text.Encoding.UTF8, "application/json"));
        var edited = await LatestChangeAsync(client);

        var note = await (await client.PostAsJsonAsync(Notes, new MapNoteRequest("Too wide.", new NoteAnchorDto(NoteAnchors.Map))))
            .Content.ReadFromJsonAsync<MapNoteDto>();
        await Assert.That(edited).IsGreaterThan(loaded);
        await Assert.That(note!.Messages[0].Change).IsEqualTo(edited)
            .Because("the layout edit is a change, and the note was written after it");

        var stated = await (await client.PostAsJsonAsync($"{Notes}/{note.Id}/replies",
            new NoteReplyRequest("Seen at the load.", Change: loaded))).Content.ReadFromJsonAsync<MapNoteDto>();
        await Assert.That(stated!.Messages[^1].Change).IsEqualTo(loaded);

        using var ahead = await client.PostAsJsonAsync($"{Notes}/{note.Id}/replies",
            new NoteReplyRequest("From the future.", Change: edited + 5));
        await Assert.That(ahead.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var finding = (await ahead.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("findings")[0];
        await Assert.That(finding.GetProperty("field").GetString()).IsEqualTo("change");
    }

    private static async Task<long> LatestChangeAsync(HttpClient client)
    {
        var changes = (await client.GetFromJsonAsync<JsonElement>($"/api/map/{SketchBoard.Slug}/changes")).GetProperty("changes");
        return changes[changes.GetArrayLength() - 1].GetProperty("number").GetInt64();
    }

    private static async Task<NotePictureDto> Keep(HttpClient client, byte[] bytes, string type)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(type);
        var resp = await client.PostAsync("/api/notes/pictures", content);
        var text = await resp.Content.ReadAsStringAsync();
        await Assert.That(resp.IsSuccessStatusCode).IsTrue().Because(text);
        return JsonSerializer.Deserialize<NotePictureDto>(text, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }
}
