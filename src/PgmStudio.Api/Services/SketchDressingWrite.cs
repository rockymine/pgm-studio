using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PgmStudio.Data.Map;
using PgmStudio.Export;
using PgmStudio.Data.Schema;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Services;

/// <summary>
/// One placement of a map's dressing, read and written without the caller holding the whole layout.
///
/// <para>The dressing rides inside the sketch layout because a prop's position is as much a part of the map as
/// a shape's, and that is the right place for it to live. It is the wrong place for it to be <em>addressed</em>
/// from: a caller adding one tree had to send back every shape, every theme and every other prop, which is
/// what <see cref="IntentWrite"/>'s half of the surface stopped doing when the objectives became resources.
/// This is that shape for the finish.</para>
///
/// <para><b>A partial write runs the whole layout's gate.</b> The document that reaches the store is the one
/// the export will read, so it answers for itself exactly as <c>PUT /sketch</c>'s does — a prop whose style is
/// a see-through roof is refused here, not discovered while the world is being built.</para>
/// </summary>
public static class SketchDressingWrite
{
    /// <summary>The stored dressing, or an empty document where the map has none. A layout that will not read
    /// as a dressing document throws <see cref="DressingParseException"/>, which the endpoint answers as
    /// <c>DR-DOC</c>.</summary>
    public static DressingDoc Read(string? layoutJson)
    {
        if (string.IsNullOrWhiteSpace(layoutJson)) return DressingDoc.Empty;
        return DressingScope.DocOf(layoutJson);
    }

    /// <summary>The layout with <paramref name="doc"/> in its <c>dressing</c> key and everything else exactly
    /// as it was stored. A key swap rather than a re-serialize of the whole layout, for the reason the GET
    /// answers the stored bytes: a round trip through the typed model drops whatever the reader has no field
    /// for, and losing an author's work to a write they did not make is what <c>RQ3</c> reports rather than
    /// causes.</summary>
    public static string With(string? layoutJson, DressingDoc doc)
    {
        var layout = string.IsNullOrWhiteSpace(layoutJson)
            ? new JsonObject()
            : JsonNode.Parse(layoutJson) as JsonObject ?? new JsonObject();
        layout["dressing"] = JsonNode.Parse(DressingJson.Serialize(doc));
        return layout.ToJsonString();
    }

    /// <summary>What a body states as one placement, or null where it states none. A body that will not read
    /// is the request's own fault (<c>RQ1</c>), answered where the body is read.</summary>
    public static PlacedProp? Stated(string json)
    {
        try { return JsonSerializer.Deserialize<PlacedProp>(json, DressingJson.Options); }
        catch (JsonException) { return null; }
    }

    /// <summary>Gate the edited layout and store it, answering the refusal where either says no. The
    /// <c>If-Match</c> guards the <b>layout</b>, which is the document the placement lives in and the one a
    /// concurrent edit would lose.</summary>
    public static async Task<DressingWritten> StoreAsync(
        MapArtifactStore artifacts, long mapId, string layoutJson, string id, long? expected, CancellationToken ct)
    {
        var findings = SketchMaterialGate.Check(layoutJson);
        if (findings.Count > 0) return new(null, id, findings);

        var written = await DocumentWrite.StoreAsync(artifacts, mapId, ArtifactKind.SketchLayoutJson,
            "sketch layout", Encoding.UTF8.GetBytes(layoutJson), expected, ct);
        return written.Refusal is { } refusal
            ? new(null, id, Findings.None, refusal)
            : new(written.Revision, id, Findings.None);
    }
}

/// <summary>The outcome of writing one placement: the layout's new revision, the id the edit acted on, and
/// whatever refused it. <see cref="Revision"/> is null exactly when nothing was stored.</summary>
public readonly record struct DressingWritten(
    long? Revision, string Id, Findings Findings, Refusal? Refusal = null);
