using System.Text;
using Microsoft.AspNetCore.Http;
using PgmStudio.Data.Map;
using PgmStudio.Api.Endpoints;
using PgmStudio.Data.Schema;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Services;

/// <summary>
/// Storing a layout one part of it was edited in — the step every addressable piece of the sketch shares.
///
/// <para><b>A partial write answers for the whole document.</b> What reaches the store is what the export
/// will read, so it runs the gate and the check <c>PUT /sketch</c> runs and in the same two registers: a
/// style or theme its own materials cannot honour <b>refuses</b> at 400, and everything the document says
/// that the build cannot is a <b>complaint</b> riding back on the 200. Anything less would make the small
/// route the way to get past the big route's gate.</para>
///
/// <para>The <c>If-Match</c> guards the layout, which is the document the edited part lives in and the one a
/// concurrent edit would lose — not the part, which has no revision of its own.</para>
/// </summary>
public static class SketchPartWrite
{
    public static async Task<PartWritten> StoreAsync(
        HttpContext http, MapArtifactStore artifacts, long mapId, string layoutJson, string id,
        CancellationToken ct)
    {
        var findings = SketchMaterialGate.Check(layoutJson);
        if (findings.Count > 0) return new(null, id, findings);

        var layout = SketchLayout.Stated(layoutJson);
        Complaints.Unread(http, layoutJson, layout);
        Complaints.Add(http, SketchLayoutCheck.Check(layout).AsComplaints());

        var written = await DocumentWrite.StoreAsync(artifacts, mapId, ArtifactKind.SketchLayoutJson,
            "sketch layout", Encoding.UTF8.GetBytes(layoutJson), Revisions.Expected(http), ct);

        return written.Refusal is { } refusal
            ? new(null, id, Findings.None, refusal)
            : new(written.Revision, id, Findings.None);
    }

    /// <summary>Whether the write was refused — the refusal is on the wire when it answers true, and the
    /// layout's new revision is on the response when it answers false. The one place a partial write's two
    /// failure registers are decided, so no two routes can disagree about what one looks like.</summary>
    public static async Task<bool> RefusedAsync(HttpContext http, PartWritten written, CancellationToken ct)
    {
        if (written.Refusal is { } refusal) { await Refusals.WriteAsync(http, refusal, ct); return true; }
        if (written.Findings.Count > 0)
            return await Refusals.StopAsync(http, 400, "invalid style or theme", written.Findings, ct);

        Revisions.Answer(http, written.Revision!.Value);
        return false;
    }

    /// <summary>The stored layout as text, or null where the map carries none.</summary>
    public static async Task<string?> LayoutOf(MapArtifactStore artifacts, long mapId, CancellationToken ct)
    {
        var data = await artifacts.LoadAsync(mapId, ArtifactKind.SketchLayoutJson, ct);
        return data is null ? null : Encoding.UTF8.GetString(data);
    }
}

/// <summary>The outcome of storing an edited layout: the layout's new revision, the id of the part that was
/// edited, and whatever refused it. <see cref="Revision"/> is null exactly when nothing was stored.</summary>
public readonly record struct PartWritten(
    long? Revision, string Id, Findings Findings, Refusal? Refusal = null);
