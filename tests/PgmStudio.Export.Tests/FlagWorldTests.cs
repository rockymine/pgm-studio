using System.Text.Json;
using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Minecraft.Stamping;
using PgmStudio.Pgm;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Export.Tests;

/// <summary>
/// End-to-end King of the Flag: an intent carrying a flag → world + map.xml. PGM loads a flag only where a
/// banner stands at its starting post, so the claim worth proving is that every post the XML names is a block
/// the world stood a banner in — read back the way an import reads one — on ground a player stands on.
/// </summary>
public sealed class FlagWorldTests
{
    private const string Json = """
        {
          "plan": 2,
          "meta": { "name": "Flag Probe" },
          "globals": { "cell": 5, "symmetry": "rot_180", "surface": 9, "headroom": 11 },
          "pieces": [
            { "id": "field", "role": "piece", "rect": [-4, -4, 4, 4], "surface": 12 }
          ]
        }
        """;

    private static FlagIntent Flag(params FlagPostIntent[] posts) =>
        new() { Name = "Flag", Color = "yellow", Posts = [.. posts] };

    private static BuiltWorld Build(FlagIntent flag)
    {
        var plan = PlanModel.Parse(Json)!;
        var (layout, intent) = PlanCompiler.Compile(plan);
        return WorldBuilder.Build(JsonSerializer.Serialize(layout, SketchLayout.Json), intent with { Flags = [flag] });
    }

    private static MapXml Written(BuiltWorld built)
    {
        var doc = new Dictionary<string, object?>();
        IntentGenerator.Apply(doc, built.ResolvedIntent);
        return MapParser.ParseXmlString(XmlWriter.ToXml(Deserializer.FromDict(doc)));
    }

    // The rot_180 centre is the point (0, 0) itself, so a post there is its own image.
    private static readonly FlagPostIntent Centre = new() { Name = "Mid", At = new Pt(0, 0, 0) };
    private static readonly FlagPostIntent East = new() { Name = "East", At = new Pt(10.5, 0, 0.5), Yaw = 270 };

    [Test]
    public async Task Every_post_stands_a_banner_an_import_reads_back_in_the_flag_colour()
    {
        var built = Build(Flag(Centre, East));
        var posts = built.ResolvedIntent.Flags!.Single().Posts;
        var banners = FeatureExtractors.Banners(AnvilRegion.FromWorld(built.World)).ToList();

        await Assert.That(posts.Count).IsEqualTo(3).Because("East gains its image under rot_180; Mid is its own");
        foreach (var post in posts)
        {
            var box = post.BannerBox!.Value;
            var banner = banners.Single(found => (found.WorldX, found.WorldY, found.WorldZ) == (box.MinX, box.MinY, box.MinZ));
            await Assert.That(banner.Wall).IsFalse();
            await Assert.That(banner.Color).IsEqualTo("yellow");
            await Assert.That(banner.Facing).IsEqualTo(FlagStamper.Rotation(post.Yaw));
        }
        await Assert.That(banners.Count).IsEqualTo(3);
    }

    [Test]
    public async Task A_banner_stands_on_ground_under_open_air()
    {
        var built = Build(Flag(Centre, East));

        foreach (var post in built.ResolvedIntent.Flags!.Single().Posts)
        {
            var box = post.BannerBox!.Value;
            await Assert.That(built.World.GetBlock(box.MinX, box.MinY, box.MinZ).Id).IsEqualTo(Blocks.StandingBanner);
            await Assert.That(built.World.GetBlock(box.MinX, box.MinY - 1, box.MinZ).Id).IsNotEqualTo(Blocks.Air);
            await Assert.That(built.World.GetBlock(box.MinX, box.MinY + 1, box.MinZ).Id).IsEqualTo(Blocks.Air);
        }
    }

    [Test]
    public async Task The_image_banner_faces_the_way_its_post_was_turned()
    {
        var built = Build(Flag(Centre, East));
        var image = built.ResolvedIntent.Flags!.Single().Posts.Single(post => post.Name == "East 2");

        await Assert.That(image.Yaw).IsEqualTo(90.0);
        var box = image.BannerBox!.Value;
        await Assert.That(built.World.GetBlock(box.MinX, box.MinY, box.MinZ).Data).IsEqualTo(4)
            .Because("a standing banner turned a quarter from south faces west");
    }

    // PGM looks at the block each point of the starting post is in, so the point has to be inside the banner.
    [Test]
    public async Task Every_post_point_the_map_names_is_inside_its_banner()
    {
        var built = Build(Flag(Centre, East));
        var flag = Written(built).Flags.Single();
        var boxes = built.ResolvedIntent.Flags!.Single().Posts.Select(post => post.BannerBox!.Value).ToList();

        await Assert.That(flag.Post!.Posts.Count).IsEqualTo(boxes.Count).Because("the map fans nothing the world did not");
        foreach (var (post, box) in flag.Post.Posts.Zip(boxes))
        {
            var point = post.Point!.Value;
            await Assert.That(((int)Math.Floor(point.X), (int)Math.Floor(point.Y), (int)Math.Floor(point.Z)))
                .IsEqualTo((box.MinX, box.MinY, box.MinZ));
        }
    }

    [Test]
    public async Task A_banner_is_written_in_the_dye_of_its_colour()
    {
        var tile = FlagStamper.Banner(0, 10, 0, "red");

        await Assert.That(tile.Get<fNbt.NbtInt>("Base")!.Value).IsEqualTo(1).Because("red dye is damage 1");
        await Assert.That(BlockColors.DyeColor(BlockColors.DyeDamage("light_blue"))).IsEqualTo("light_blue");
    }
}
