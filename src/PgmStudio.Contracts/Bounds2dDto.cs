using System.Text.Json.Serialization;

namespace PgmStudio.Contracts;

/// <summary>A footprint in block coordinates — the <c>bounds_2d</c> of the contract, and the studio's one
/// box. Four numbers, always: unlike a region <see cref="RegionExtentDto"/>, which may be unbounded on a
/// side, this comes from a stored box, a measured island footprint, a raster the studio bounded or the
/// ground a search is asked to read, which is why it is both an answer and a request.</summary>
/// <param name="MinX">The west edge.</param>
/// <param name="MinZ">The north edge.</param>
/// <param name="MaxX">The east edge.</param>
/// <param name="MaxZ">The south edge.</param>
public sealed record Bounds2dDto(
    [property: JsonPropertyName("min_x")] double MinX,
    [property: JsonPropertyName("min_z")] double MinZ,
    [property: JsonPropertyName("max_x")] double MaxX,
    [property: JsonPropertyName("max_z")] double MaxZ);
