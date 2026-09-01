using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Components;

/// <summary>
/// What a terrain material kind <b>is</b>, read from the schema the API publishes rather than restated here.
///
/// <para><c>GET /api/terrain/patterns</c> answers every kind, its label, a sentence on what it draws, the cell
/// facts it varies with, and its fields with their wire types and defaults — and the server reads all of that
/// off the records' own <c>JsonDerivedType</c> attributes and primary constructors, so it cannot offer a kind
/// the deserializer would reject or miss one it accepts. A second copy on this side of the wire is exactly the
/// drift the route exists to prevent: a kind added server-side reached no editor until someone remembered to
/// add it twice, and a kind the editor offered but could not seed became a stone block when it was chosen.</para>
///
/// <para><b>What stays this side is what the schema is not.</b> The schema says a field is an <c>int</c>; it
/// does not say a fresh voronoi wants a grid line, a thin course and a body, because that is the editor's
/// judgement about a good starting point rather than a fact about the format. Those live in
/// <see cref="ThemeFields.Starters"/>, keyed by field name so a kind reusing <c>seed</c> or <c>scale</c> gets a
/// sensible value without being named at all.</para>
/// </summary>
public sealed class MaterialSchema(HttpClient http)
{
    private IReadOnlyList<MaterialKindDto> kinds = [];

    /// <summary>Every kind the painter accepts, in the order the route offers them. Empty until loaded, which
    /// renders an empty picker rather than a wrong one.</summary>
    public IReadOnlyList<MaterialKindDto> Kinds => kinds;

    /// <summary>Load once per session. The schema is a build constant — it changes when the deserializer does
    /// — so re-fetching it per editor would be a round trip for an answer that cannot have moved.</summary>
    public async Task LoadAsync()
    {
        if (kinds.Count > 0) return;
        try { kinds = await http.GetFromJsonAsync<List<MaterialKindDto>>("api/terrain/patterns") ?? []; }
        catch { kinds = []; }
    }

    public MaterialKindDto? Of(string kind) => kinds.FirstOrDefault(entry => entry.Kind == kind);

    /// <summary>The label a kind is offered under, or the raw discriminator for one the schema did not carry.</summary>
    public string NameOf(string kind) => Of(kind)?.Name ?? kind;

    /// <summary>What the kind draws, in its own words — the sentence the schema publishes, so the editor's help
    /// and an agent reading the route are told the same thing.</summary>
    public string? SummaryOf(string kind) => Of(kind)?.Summary;

    /// <summary>Which facts about a cell the kind varies with, and therefore where it is legible at all: one
    /// reading <c>arc</c> says nothing away from a perimeter, one reading <c>inset</c> draws rings.</summary>
    public IReadOnlyList<string> ReadsOf(string kind) => Of(kind)?.Reads ?? [];

    /// <summary>The kind's single nested materials — a tint's neutral, a checker's two squares, a frame's edge
    /// and panel. Read off the field types: a field typed <c>material</c> is one.</summary>
    public IReadOnlyList<string> ChildrenOf(string kind) =>
        [.. (Of(kind)?.Fields ?? []).Where(f => f.Type == "material").Select(f => f.Name)];

    /// <summary>The kind's one list of materials, as the path that reaches it and the key its entries claim an
    /// extent under — or null for a kind with no list. Read off the field type, which is what says both: a
    /// <c>bandStack</c> holds its bands one level down and measures them in thickness, a <c>voronoiBand[]</c>
    /// measures in depth, a <c>wallStripe[]</c> in width, and a plain <c>material[]</c> has no extent.</summary>
    public (string Path, string? Extent)? ListOf(string kind)
    {
        foreach (var field in Of(kind)?.Fields ?? [])
            switch (field.Type)
            {
                case "bandStack": return ($"{field.Name}/{ThemeFields.Bands}", ThemeFields.Thickness);
                case "voronoiBand[]": return (field.Name, ThemeFields.Depth);
                case "wallStripe[]": return (field.Name, ThemeFields.Width);
                case "material[]": return (field.Name, null);
            }
        return null;
    }

    /// <summary>A fresh node of the kind: every field the schema names, at the value the schema states or the
    /// one the editor starts it at. A kind the schema did not carry falls back to a solid block, which is the
    /// one node every other kind bottoms out in.</summary>
    public JsonObject Seed(string kind)
    {
        if (Of(kind) is not { } info) return ThemeFields.Solid(1);

        var node = new JsonObject { [ThemeFields.Kind] = kind };
        foreach (var field in info.Fields)
        {
            if (ThemeFields.Starter(kind, field.Name, field.Type) is { } starter) { node[field.Name] = starter; continue; }
            if (Stated(field) is { } stated) node[field.Name] = stated;
        }
        return node;
    }

    /// <summary>The schema's own default for an optional field, as a node. A required field with no default is
    /// the editor's to start, which <see cref="ThemeFields.Starter"/> answers.</summary>
    private static JsonNode? Stated(MaterialFieldDto field) => field.Default switch
    {
        null => null,
        JsonElement { ValueKind: JsonValueKind.Number } number => JsonValue.Create(number.GetInt32()),
        JsonElement { ValueKind: JsonValueKind.String } text => JsonValue.Create(text.GetString()),
        JsonElement { ValueKind: JsonValueKind.True } => JsonValue.Create(true),
        JsonElement { ValueKind: JsonValueKind.False } => JsonValue.Create(false),
        _ => null,
    };
}
