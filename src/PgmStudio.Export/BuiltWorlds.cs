using System.Buffers;
using System.Text;
using System.Text.Json;
using PgmStudio.Geom;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Export;

/// <summary>
/// <see cref="WorldBuilder.Build"/>, built once per distinct pair of documents and shared by every caller that
/// asks for the same world — a render, a walk, the columns and the export of one board are one build.
/// <para>The key is the layout and the intent as it serializes. Every <c>[JsonIgnore]</c> on the intent's types
/// is derived from the fields beside it, so the serialized form carries everything the build reads.</para>
/// <para><b>A shared world is read, never written.</b> A caller that stamps onto a world builds its own with
/// <see cref="WorldBuilder.Build"/>.</para>
/// </summary>
public static class BuiltWorlds
{
    /// <summary>The board being read and the few it was compared against.</summary>
    private static readonly Remembered<BuiltWorld> Worlds = new(capacity: 4);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The world these documents build — the one already built where they have been asked before.</summary>
    public static BuiltWorld Of(string layoutJson, MapIntent intent)
    {
        var key = new ArrayBufferWriter<byte>(layoutJson.Length + 2048);
        Compact(layoutJson, key);
        key.Write<byte>([0]);
        key.Write(JsonSerializer.SerializeToUtf8Bytes(intent, Json));
        return Worlds.Of(key.WrittenSpan, () => WorldBuilder.Build(layoutJson, intent));
    }

    /// <summary>The layout with its insignificant whitespace taken out, so the stored document and the same
    /// document posted back pretty-printed are one world. Property order, repeated names and every number's
    /// own spelling are kept; text that is not JSON is keyed as it stands.</summary>
    private static void Compact(string layoutJson, IBufferWriter<byte> into)
    {
        try
        {
            using var document = JsonDocument.Parse(layoutJson);
            using var writer = new Utf8JsonWriter(into);
            document.WriteTo(writer);
        }
        catch (JsonException)
        {
            into.Write(Encoding.UTF8.GetBytes(layoutJson));
        }
    }
}
