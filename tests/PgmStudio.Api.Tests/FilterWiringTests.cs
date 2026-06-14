using PgmStudio.Api.Services;

namespace PgmStudio.Api.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>F1 wiring-template tests: apply emits standard Filter + ApplyRule (+ compound) entries
/// through the editors onto the caller-chosen region. Pure logic over a doc dict (no HTTP/DB).</summary>
public sealed class FilterWiringTests
{
    [Test]
    public async Task Applies_spawn_protection_to_the_given_region()
    {
        var doc = new Dict
        {
            ["teams"] = new List<object?> { new Dict { ["id"] = "red-team", ["color"] = "red" } },
            // the caller targets the spawn AREA (not the point — the point is never wired, by contract)
            ["regions"] = new Dict { ["red-spawn"] = new Dict { ["id"] = "red-spawn", ["type"] = "rectangle" } },
            ["filters"] = new Dict(),
            ["apply_rules"] = new List<object?>(),
        };

        FilterWiring.ApplyTemplate(doc, "spawn_protection", new Dict { ["region"] = "red-spawn", ["team"] = "red-team" });

        var filters = (Dict)doc["filters"]!;
        await Assert.That(filters.ContainsKey("only-red")).IsTrue();
        await Assert.That(((Dict)filters["only-red"]!)["type"]).IsEqualTo("team");

        var rule = ((List<object?>)doc["apply_rules"]!).OfType<Dict>()
            .First(r => r.GetValueOrDefault("region") as string == "red-spawn");
        await Assert.That(rule["enter"]).IsEqualTo("only-red");
        await Assert.That(rule["message"]).IsEqualTo("You may not enter the enemy's spawn!");
    }

    [Test]
    public async Task Build_void_enforcement_groups_negative_and_denies_void()
    {
        var doc = new Dict
        {
            ["regions"] = new Dict
            {
                ["build-a"] = new Dict { ["id"] = "build-a", ["type"] = "rectangle" },
                ["build-b"] = new Dict { ["id"] = "build-b", ["type"] = "rectangle" },
            },
            ["filters"] = new Dict(),
            ["apply_rules"] = new List<object?>(),
        };

        var res = FilterWiring.ApplyTemplate(doc, "build_void_enforcement",
            new Dict { ["build_region_ids"] = new List<object?> { "build-a", "build-b" } });

        // a negative compound wrapping the build regions
        var negId = (string)res["region_id"]!;
        var neg = (Dict)((Dict)doc["regions"]!)[negId]!;
        await Assert.That(neg["type"]).IsEqualTo("negative");
        var kids = ((List<object?>)neg["children"]!).Select(x => x?.ToString()).ToList();
        await Assert.That(kids).Contains("build-a");
        await Assert.That(kids).Contains("build-b");

        // …with a placement rule denying the void on it
        var rule = ((List<object?>)doc["apply_rules"]!).OfType<Dict>()
            .First(r => r.GetValueOrDefault("region") as string == negId);
        await Assert.That(rule["block_place"]).IsEqualTo("deny(void)");
    }

    [Test]
    public async Task Wool_room_defense_excludes_the_owner()
    {
        var doc = new Dict
        {
            ["teams"] = new List<object?> { new Dict { ["id"] = "red-team", ["color"] = "red" } },
            ["regions"] = new Dict { ["red-woolroom"] = new Dict { ["id"] = "red-woolroom", ["type"] = "cuboid" } },
            ["filters"] = new Dict(),
            ["apply_rules"] = new List<object?>(),
        };

        FilterWiring.ApplyTemplate(doc, "wool_room_defense", new Dict { ["region"] = "red-woolroom", ["owner"] = "red-team" });

        // not-red filter (owner excluded) wired as enter
        await Assert.That(((Dict)doc["filters"]!).ContainsKey("not-red")).IsTrue();
        await Assert.That(((Dict)((Dict)doc["filters"]!)["not-red"]!)["type"]).IsEqualTo("not");
        var rule = ((List<object?>)doc["apply_rules"]!).OfType<Dict>().First(r => r.GetValueOrDefault("region") as string == "red-woolroom");
        await Assert.That(rule["enter"]).IsEqualTo("not-red");
    }
}
