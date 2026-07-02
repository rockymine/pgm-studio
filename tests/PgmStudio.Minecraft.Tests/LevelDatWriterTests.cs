using fNbt;
using PgmStudio.Minecraft;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// <c>level.dat</c> round-trips: it is a gzipped NBT file with a <c>Data</c> compound carrying the world
/// spawn, level name, flat generator, and a non-zero creation timestamp.
/// </summary>
public sealed class LevelDatWriterTests
{
    [Test]
    public async Task Writes_a_gzipped_level_dat_with_spawn_name_and_timestamp()
    {
        var dir = Path.Combine(Path.GetTempPath(), "leveldat_" + Guid.NewGuid().ToString("N"));
        try
        {
            LevelDatWriter.Write(dir, "outback", spawnX: 12, spawnY: 70, spawnZ: -8, lastPlayedMs: 1_700_000_000_000);

            var path = Path.Combine(dir, "level.dat");
            await Assert.That(File.Exists(path)).IsTrue();

            // gzip magic — the file must be compressed, not raw NBT
            var head = File.ReadAllBytes(path)[..2];
            await Assert.That(head[0]).IsEqualTo((byte)0x1f);
            await Assert.That(head[1]).IsEqualTo((byte)0x8b);

            var file = new NbtFile();
            file.LoadFromFile(path);
            var data = file.RootTag.Get<NbtCompound>("Data")!;

            await Assert.That(data.Get<NbtString>("LevelName")!.Value).IsEqualTo("outback");
            await Assert.That(data.Get<NbtInt>("SpawnX")!.Value).IsEqualTo(12);
            await Assert.That(data.Get<NbtInt>("SpawnY")!.Value).IsEqualTo(70);
            await Assert.That(data.Get<NbtInt>("SpawnZ")!.Value).IsEqualTo(-8);
            await Assert.That(data.Get<NbtLong>("LastPlayed")!.Value).IsEqualTo(1_700_000_000_000L);
            await Assert.That(data.Get<NbtString>("generatorName")!.Value).IsEqualTo("flat");
            await Assert.That(data.Get<NbtInt>("version")!.Value).IsEqualTo(19133);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
