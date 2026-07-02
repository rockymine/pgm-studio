using fNbt;

namespace PgmStudio.Minecraft;

/// <summary>
/// Writes a 1.8–1.12 <c>level.dat</c> (gzipped NBT) for a synthesised world: a <c>Data</c> compound with
/// the world spawn, a flat/void generator, a level name, and a real creation timestamp (<c>LastPlayed</c> —
/// a zero date trips some loaders). Daylight cycle and mob spawning are disabled, matching a CTW arena.
/// </summary>
public static class LevelDatWriter
{
    /// <summary>The Anvil region-format version tag carried in <c>Data.version</c> for 1.8–1.12 worlds.</summary>
    private const int AnvilVersion = 19133;

    /// <summary>
    /// Write <c>{worldDir}/level.dat</c>. World spawn is the observer/default spawn point;
    /// <paramref name="lastPlayedMs"/> is the creation time in Unix milliseconds.
    /// </summary>
    public static void Write(string worldDir, string levelName, int spawnX, int spawnY, int spawnZ, long lastPlayedMs)
    {
        Directory.CreateDirectory(worldDir);

        var gameRules = new NbtCompound("GameRules")
        {
            new NbtString("doDaylightCycle", "false"),
            new NbtString("doMobSpawning", "false"),
            new NbtString("doWeatherCycle", "false"),
        };

        var data = new NbtCompound("Data")
        {
            new NbtInt("version", AnvilVersion),
            new NbtByte("initialized", 1),
            new NbtString("LevelName", levelName),
            new NbtString("generatorName", "flat"),
            new NbtInt("generatorVersion", 0),
            new NbtString("generatorOptions", "3;0;1;"),   // single air layer → void outside the built chunks
            new NbtLong("RandomSeed", 0),
            new NbtByte("MapFeatures", 0),
            new NbtByte("hardcore", 0),
            new NbtInt("GameType", 0),
            new NbtByte("allowCommands", 1),
            new NbtByte("Difficulty", 2),
            new NbtByte("DifficultyLocked", 0),
            new NbtInt("SpawnX", spawnX),
            new NbtInt("SpawnY", spawnY),
            new NbtInt("SpawnZ", spawnZ),
            new NbtLong("Time", 0),
            new NbtLong("DayTime", 0),
            new NbtLong("LastPlayed", lastPlayedMs),
            new NbtByte("raining", 0),
            new NbtInt("rainTime", 0),
            new NbtByte("thundering", 0),
            new NbtInt("thunderTime", 0),
            gameRules,
        };

        var root = new NbtCompound("") { data };
        new NbtFile(root).SaveToFile(Path.Combine(worldDir, "level.dat"), NbtCompression.GZip);
    }
}
