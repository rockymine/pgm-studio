using System.Text.Json;
using System.Text.Json.Serialization;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// The JSON the wire is written in, stated once for the two parties that have to agree on it: the endpoints
/// that write an answer and the generator that publishes what an answer is. A setting stated on one and not
/// the other is a schema describing a different wire.
/// </summary>
internal static class WireJson
{
    /// <summary>A closed set crosses as its words, camelCased: <c>"solid"</c>, never <c>0</c>.</summary>
    public static void Configure(JsonSerializerOptions json) =>
        json.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
}
