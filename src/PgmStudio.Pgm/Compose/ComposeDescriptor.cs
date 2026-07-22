using System.Text.Json;
using System.Text.Json.Serialization;

namespace PgmStudio.Pgm.Compose;

/// <summary>The composer's output version. Stamped onto every generated plan's descriptor so a stored row is
/// honest about which composer made it: a descriptor reproduces its plan exactly within one version, and only
/// approximately across versions. Bump when a change to the pipeline alters the geometry a given
/// (request, seed) produces.</summary>
public static class ComposerVersion
{
    public const string Current = "box-1";
}

/// <summary>
/// The canonical, versioned request that reproduces a generated plan: the composer version plus every field
/// of the <see cref="ComposeRequest"/> (including the seed). This is a generated plan's identity — stored in
/// the plan row's <c>request_json</c> and used as the card key in the browse feed. <see cref="Schema"/> is the
/// descriptor's own shape version, bumped if these fields change, so an old stored descriptor still reads.
/// </summary>
public sealed record ComposeDescriptor(
    int Schema,
    string ComposerVersion,
    int PlayersPerTeam,
    int Teams,
    string Symmetry,
    int Cell,
    ulong Seed)
{
    /// <summary>The current descriptor schema version.</summary>
    public const int CurrentSchema = 1;

    /// <summary>Build the descriptor for a request under the current composer + schema version.</summary>
    public static ComposeDescriptor For(ComposeRequest request) => new(
        CurrentSchema, Compose.ComposerVersion.Current,
        request.PlayersPerTeam, request.Teams, request.Symmetry, request.Cell, request.Seed);

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    public static ComposeDescriptor? Parse(string json) =>
        JsonSerializer.Deserialize<ComposeDescriptor>(json, Json);
}
