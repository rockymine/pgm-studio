using System.Text.Json.Nodes;
using PgmStudio.Contracts;

namespace PgmStudio.Api.Endpoints;

/// <summary>What a posted <c>bounds</c> object turned out to be.</summary>
internal enum PostedBoundsKind
{
    /// <summary>No <c>bounds</c> key at all.</summary>
    Absent,

    /// <summary>A <c>bounds</c> object short of at least one corner.</summary>
    Incomplete,

    /// <summary>Four corners, read.</summary>
    Read,
}

/// <summary>
/// Read the <c>{"bounds": {…}}</c> object the two search routes post, as the one place that spells a corner.
///
/// <para>The binder cannot answer this. A <see cref="Bounds2dDto"/>'s four corners are non-nullable
/// <c>double</c>s, so <see cref="RequiredFields"/> skips them and a corner left out binds to zero — a box
/// short of a side and a box at the origin arrive identical. The three answers a caller can get are
/// different faults with different statuses, so the body is read as JSON here and they are told apart.</para>
/// </summary>
internal static class PostedBounds
{
    /// <summary>The four keys a corner is spelled with, for a refusal that names them.</summary>
    public const string Corners = "min_x, min_z, max_x, max_z";

    public static async Task<(PostedBoundsKind Kind, Bounds2dDto? Bounds)> ReadAsync(HttpContext http, CancellationToken ct)
    {
        if ((JsonNode.Parse(await RawBody.ReadAsync(http, ct)) as JsonObject)?["bounds"] is not JsonObject posted)
            return (PostedBoundsKind.Absent, null);

        double? Corner(string key) => posted[key]?.GetValue<double>();
        if (Corner("min_x") is not { } minX || Corner("min_z") is not { } minZ
            || Corner("max_x") is not { } maxX || Corner("max_z") is not { } maxZ)
            return (PostedBoundsKind.Incomplete, null);

        return (PostedBoundsKind.Read, new Bounds2dDto(minX, minZ, maxX, maxZ));
    }
}
