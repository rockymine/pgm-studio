namespace PgmStudio.Pgm;

/// <summary>
/// Thrown by <see cref="MapParser"/> when a map.xml is outside the studio's supported range: a
/// <c>proto</c> below the id-based regions/filters/kits floor (1.4.0), or a map declaring a modern
/// (1.13+) <c>min-server-version</c> whose world uses the palette block format the Anvil reader cannot
/// decode. Batch scanners catch this to skip-and-log the map; it is not a malformed-XML error.
/// </summary>
public sealed class UnsupportedMapException(string message) : Exception(message);
