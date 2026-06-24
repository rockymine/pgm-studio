using System.Text.Json;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// The wire contract for the intent store: the client serialises rooms/protection as camelCase JSON arrays
/// of <c>{minX,minZ,maxX,maxZ}</c>, and the endpoint deserialises with <see cref="JsonSerializerDefaults.Web"/>
/// (camelCase + case-insensitive). These guard that a multi-rect <c>room</c>/<c>protection</c> array maps to
/// the <see cref="List{Rect}"/> the generators consume — and that an absent array stays an empty list.
/// </summary>
public sealed class MapIntentJsonTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Test]
    public async Task Deserializes_multi_rect_protection_and_room_arrays()
    {
        const string json = """
        {
          "spawns": [
            { "team": "red-team", "point": { "x": 0, "y": 8, "z": 0 },
              "protection": [ { "minX": 0, "minZ": 0, "maxX": 10, "maxZ": 10 }, { "minX": 10, "minZ": 0, "maxX": 20, "maxZ": 5 } ] }
          ],
          "wools": [
            { "owner": "red-team", "color": "red", "spawn": { "x": 5, "y": 10, "z": 5 },
              "room": [ { "minX": 0, "minZ": 0, "maxX": 10, "maxZ": 10 }, { "minX": 10, "minZ": 0, "maxX": 16, "maxZ": 6 } ] }
          ]
        }
        """;
        var intent = JsonSerializer.Deserialize<MapIntent>(json, Web)!;

        var prot = intent.Spawns.Single().Protection;
        await Assert.That(prot.Count).IsEqualTo(2);
        await Assert.That(prot[0]).IsEqualTo(new Rect(0, 0, 10, 10));
        await Assert.That(prot[1]).IsEqualTo(new Rect(10, 0, 20, 5));

        var room = intent.Wools!.Single().Room;
        await Assert.That(room.Count).IsEqualTo(2);
        await Assert.That(room[1]).IsEqualTo(new Rect(10, 0, 16, 6));
    }

    [Test]
    public async Task Absent_protection_and_room_default_to_empty_lists()
    {
        const string json = """
        {
          "spawns": [ { "team": "red-team", "point": { "x": 0, "y": 8, "z": 0 } } ],
          "wools": [ { "owner": "red-team", "color": "red", "spawn": { "x": 5, "y": 10, "z": 5 } } ]
        }
        """;
        var intent = JsonSerializer.Deserialize<MapIntent>(json, Web)!;

        await Assert.That(intent.Spawns.Single().Protection.Count).IsEqualTo(0);
        await Assert.That(intent.Wools!.Single().Room.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Tolerates_legacy_single_object_protection_and_room()
    {
        // Intents authored before rooms/protection became unions stored a single {minX,…} object (or null).
        // These must still deserialize (a one-element list / empty list), not throw.
        const string json = """
        {
          "spawns": [
            { "team": "red-team", "point": { "x": 0, "y": 8, "z": 0 }, "protection": { "minX": -89, "minZ": -8, "maxX": -61, "maxZ": 8 } },
            { "team": "blue-team", "point": { "x": 0, "y": 8, "z": 0 }, "protection": null }
          ],
          "wools": [
            { "owner": "red-team", "color": "red", "spawn": { "x": 5, "y": 10, "z": 5 },
              "room": { "minX": -112, "minZ": -36, "maxX": -100, "maxZ": -24 } }
          ]
        }
        """;
        var intent = JsonSerializer.Deserialize<MapIntent>(json, Web)!;

        await Assert.That(intent.Spawns[0].Protection.Count).IsEqualTo(1);
        await Assert.That(intent.Spawns[0].Protection[0]).IsEqualTo(new Rect(-89, -8, -61, 8));
        await Assert.That(intent.Spawns[1].Protection.Count).IsEqualTo(0);   // legacy null → empty list
        await Assert.That(intent.Wools!.Single().Room.Count).IsEqualTo(1);
        await Assert.That(intent.Wools!.Single().Room[0]).IsEqualTo(new Rect(-112, -36, -100, -24));
    }

    [Test]
    public async Task Roundtrips_through_the_web_serializer_as_camelcase_arrays()
    {
        var intent = new MapIntent
        {
            Spawns = [new SpawnIntent { Team = "red-team", Point = new(0, 8, 0), Protection = [new(0, 0, 10, 10), new(10, 0, 20, 5)] }],
        };
        var json = JsonSerializer.Serialize(intent, Web);
        await Assert.That(json).Contains("\"protection\":[{\"minX\":0");

        var back = JsonSerializer.Deserialize<MapIntent>(json, Web)!;
        await Assert.That(back.Spawns.Single().Protection.Count).IsEqualTo(2);
    }
}
