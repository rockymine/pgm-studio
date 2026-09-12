using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PgmStudio.Pgm;

namespace PgmStudio.Api.Tests;

/// <summary>
/// Authoring a board with a shop over HTTP — the surface an agent drives, end to end. There is no new
/// endpoint for it: <c>PUT /map/{slug}/intent</c> takes the whole intent, and a board that sells things is
/// that intent carrying <c>shops</c>.
///
/// <para>The flow is the one the <c>pgm-board</c> skill runs — compile a plan, store its layout, finish it,
/// then state the intent — with the shop stated on that last call. What is proved here and nowhere else is
/// that the wire shape an agent posts is the one the record deserializes from, and that
/// <c>GET …/xml</c> answers a map PGM would load with the menu in it and a keeper at every spawn.</para>
///
/// <para>Runs against the <c>pgm_studio_test</c> schema, so it runs serially with the other DB suites.</para>
/// </summary>
[NotInParallel("api-db")]
public sealed class ShopIntentTests
{
    // What an agent adds to a compiled board: one shop, one tab, three things to buy, and the villager that
    // opens it. Nothing says where the villager stands — the studio puts one at every team's spawn.
    private const string Shops = """
        [
          {
            "id": "item-shop",
            "name": "Quartermaster",
            "keeper": { "name": "`b`lQuartermaster", "mob": "Villager" },
            "categories": [
              {
                "id": "blocks",
                "material": "hard clay",
                "name": "`aBuilding",
                "items": [
                  { "material": "wood", "amount": 32, "price": 1, "currency": "gold nugget" },
                  { "material": "stained clay", "amount": 16, "price": 1, "currency": "gold nugget", "teamColor": true },
                  { "material": "golden apple", "name": "`6Golden Apple", "price": 2, "currency": "gold nugget" }
                ]
              }
            ]
          }
        ]
        """;

    private static async Task<(HttpClient Client, string Slug)> ShopBoardAsync(string? shops = Shops)
    {
        await ApiTestFactory.ResetSchemaAsync();
        var client = ApiTestFactory.Shared.CreateClient();

        var compile = await client.PostAsync("/api/plan/compile",
            new StringContent(ReadSeed("base-2wool.plan.json"), Encoding.UTF8, "application/json"));
        await Assert.That(compile.IsSuccessStatusCode).IsTrue().Because(await compile.Content.ReadAsStringAsync());
        var compiled = await compile.Content.ReadFromJsonAsync<JsonElement>();

        var create = await client.PostAsJsonAsync("/api/sketch", new { name = "Shop Board" });
        var slug = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;

        var layout = await client.PutAsync($"/api/map/{slug}/sketch",
            new StringContent(compiled.GetProperty("layout").GetRawText(), Encoding.UTF8, "application/json"));
        await Assert.That(layout.IsSuccessStatusCode).IsTrue().Because(await layout.Content.ReadAsStringAsync());

        var finish = await client.PostAsync($"/api/map/{slug}/sketch/finish", null);
        await Assert.That(finish.IsSuccessStatusCode).IsTrue().Because(await finish.Content.ReadAsStringAsync());

        var stored = await client.PutAsync($"/api/map/{slug}/intent",
            new StringContent(WithShops(compiled.GetProperty("intent"), shops), Encoding.UTF8, "application/json"));
        await Assert.That(stored.IsSuccessStatusCode).IsTrue().Because(await stored.Content.ReadAsStringAsync());

        return (client, slug);
    }

    /// <summary>The compiled intent with the shop stated on it, and an author so the header board is not
    /// blank. This is the one call an agent makes differently to author a board that sells things.</summary>
    private static string WithShops(JsonElement intent, string? shops)
    {
        var node = JsonNode.Parse(intent.GetRawText())!.AsObject();
        var meta = node["meta"]?.AsObject() ?? [];
        meta["authors"] = new JsonArray(new JsonObject { ["name"] = "rockymine" });
        node["meta"] = meta;
        if (shops is not null) node["shops"] = JsonNode.Parse(shops);
        return node.ToJsonString();
    }

    private static async Task<string> XmlAsync(HttpClient client, string slug)
    {
        var response = await client.GetAsync($"/api/map/{slug}/xml");
        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(response.IsSuccessStatusCode).IsTrue().Because(body);
        return body;
    }

    /// <summary>The intent an agent posted is the intent the studio stores, the shop included.</summary>
    [Test]
    public async Task The_posted_shop_is_stored_on_the_intent()
    {
        var (client, slug) = await ShopBoardAsync();
        using var _ = client;

        var intent = await client.GetFromJsonAsync<JsonElement>($"/api/map/{slug}/intent");
        var shop = intent.GetProperty("shops").EnumerateArray().Single();
        await Assert.That(shop.GetProperty("id").GetString()).IsEqualTo("item-shop");
        await Assert.That(shop.GetProperty("keeper").GetProperty("mob").GetString()).IsEqualTo("Villager");
        await Assert.That(shop.GetProperty("categories").EnumerateArray().Single()
            .GetProperty("items").GetArrayLength()).IsEqualTo(3);
    }

    /// <summary>
    /// The whole claim: a board an agent stated as one shop exports as a map with that menu in it and a
    /// keeper standing at each team's spawn, and every part of it PGM needs is there.
    /// </summary>
    [Test]
    public async Task The_board_exports_with_its_shop_and_a_keeper_at_every_spawn()
    {
        var (client, slug) = await ShopBoardAsync();
        using var _ = client;

        var map = MapParser.ParseXmlString(await XmlAsync(client, slug));

        var shop = map.Shops.Single();
        await Assert.That(shop.Id).IsEqualTo("item-shop");
        await Assert.That(shop.Name).IsEqualTo("Quartermaster");

        var category = shop.Categories.Single();
        await Assert.That(category.Id).IsEqualTo("blocks");
        await Assert.That(category.Icon.Material).IsEqualTo("hard clay");
        await Assert.That(category.Icons.Count).IsEqualTo(3);
        await Assert.That(category.Icons.Select(i => i.Item.Material))
            .IsEquivalentTo(new[] { "wood", "stained clay", "golden apple" });
        await Assert.That(category.Icons.Single(i => i.Item.Material == "stained clay").Item.TeamColor).IsTrue();
        await Assert.That(category.Icons.Single(i => i.Item.Material == "wood").Payments.Single().Currency)
            .IsEqualTo("gold nugget");

        // One keeper per team, each naming the shop it opens. PGM spawns the entity itself, so this element
        // is the whole of it — there is nothing in the world to check.
        await Assert.That(map.Shopkeepers.Count).IsEqualTo(map.Spawns.Count);
        await Assert.That(map.Shopkeepers.Select(k => k.ShopId).Distinct()).IsEquivalentTo(new[] { "item-shop" });

        foreach (var keeper in map.Shopkeepers)
        {
            await Assert.That(keeper.Mob).IsEqualTo("Villager");
            await Assert.That(keeper.Location).IsNotNull();
            await Assert.That(keeper.Yaw).IsNotNull();
        }

        // And each stands at its own team's spawn rather than all of them at one: the keepers are as far
        // apart as the spawns are.
        var byZ = map.Shopkeepers.Select(k => k.Location!.Value.Z).ToList();
        await Assert.That(byZ.Distinct().Count()).IsEqualTo(map.Spawns.Count);
    }

    /// <summary>A board stating no shop is the board it was before — the slice is opt-in, and a plain wool
    /// map does not grow a menu by having the field exist.</summary>
    [Test]
    public async Task A_board_with_no_shop_exports_unchanged()
    {
        var (client, slug) = await ShopBoardAsync(shops: null);
        using var _ = client;

        var xml = await XmlAsync(client, slug);
        await Assert.That(xml).DoesNotContain("<shops");
        await Assert.That(xml).DoesNotContain("<shopkeeper");
        await Assert.That(MapParser.ParseXmlString(xml).Shops).IsEmpty();
    }

    private static string ReadSeed(string file)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "tools", "seeds", file);
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new FileNotFoundException($"seed not found: {file}");
    }
}
