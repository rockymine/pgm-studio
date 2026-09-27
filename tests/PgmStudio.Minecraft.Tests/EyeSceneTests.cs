using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Render;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// The eye read over a small made-up world: a stone floor at y 10 and whatever each test stands on it, drawn
/// with sprites the test paints itself. What is held is what the picture is for — the eye sees the block in
/// front of it in that block's own sprite, the flat twin keeps each sprite's mean and loses its texture, a
/// see-through texel lets the ray on, and a camera asked to frame a thing ends up seeing it.
/// </summary>
public sealed class EyeSceneTests
{
    private const int Stone = 1, Cobblestone = 4, Glass = 20, GoldBlock = 41, StoneSlab = 44, OakFence = 85, OakLog = 17, Floor = 10;

    private static BlockTextureSet Sprites(bool withStone = true)
    {
        var sprites = new Dictionary<string, BlockSprite>
        {
            ["gold_block"] = Uniform(200, 180, 0),
            ["cobblestone"] = Uniform(90, 90, 90),
            ["glass"] = new(2, new byte[16]),
            ["planks_oak"] = Uniform(150, 120, 70),
            ["log_oak_top"] = Uniform(180, 150, 100),
            ["log_oak"] = new(2, [40, 40, 40, 255, 200, 200, 200, 255, 40, 40, 40, 255, 200, 200, 200, 255]),
            ["stone_slab_top"] = Uniform(120, 120, 120),
            ["stone_slab_side"] = Uniform(120, 120, 120),
        };
        if (withStone) sprites["stone"] = Checker();
        return BlockTextureSet.Of(sprites);
    }

    private static BlockSprite Uniform(byte red, byte green, byte blue) =>
        new(2, [red, green, blue, 255, red, green, blue, 255, red, green, blue, 255, red, green, blue, 255]);

    /// <summary>Black and white texels, alternating: a sprite whose mean is grey and whose texture is all the
    /// variation there is.</summary>
    private static BlockSprite Checker()
    {
        var rgba = new byte[8 * 8 * 4];
        for (var i = 0; i < 64; i++)
        {
            var bright = ((i % 8) + (i / 8)) % 2 == 0 ? (byte)250 : (byte)0;
            rgba[i * 4] = rgba[i * 4 + 1] = rgba[i * 4 + 2] = bright;
            rgba[i * 4 + 3] = 255;
        }
        return new BlockSprite(8, rgba);
    }

    private static VoxelWorld FloorWorld()
    {
        var world = new VoxelWorld();
        for (var x = 0; x < 48; x++)
            for (var z = 0; z < 48; z++)
                world.SetBlock(x, Floor, z, Stone);
        return world;
    }

    private static (int Red, int Green, int Blue) Centre(EyePicture picture)
    {
        var at = (picture.Height / 2 * picture.Width + picture.Width / 2) * 3;
        return (picture.Rgb[at], picture.Rgb[at + 1], picture.Rgb[at + 2]);
    }

    /// <summary>The gold block's north face, seen head on, in its own colour dimmed by the shade a face
    /// pointing along z takes.</summary>
    private static EyeCamera FacingTheGold => new(16.5, Floor + 2.62, 12.5, Yaw: 0, Pitch: 8, Fov: 40);

    [Test]
    public async Task The_eye_sees_the_block_in_front_of_it_in_that_blocks_own_sprite()
    {
        var world = FloorWorld();
        world.SetBlock(16, Floor + 1, 20, GoldBlock);

        var picture = EyeScene.Of(world, Sprites()).Draw(FacingTheGold, 64, 36);

        await Assert.That(Centre(picture)).IsEqualTo((160, 144, 0));
        await Assert.That(picture.Seen.Select(seen => seen.Id)).Contains(GoldBlock);
    }

    [Test]
    public async Task The_flat_twin_keeps_each_sprites_mean_and_loses_its_texture()
    {
        var down = new EyeCamera(24.5, Floor + 6, 24.5, Yaw: 0, Pitch: 89, Fov: 40);

        var textured = EyeScene.Of(FloorWorld(), Sprites()).Draw(down, 40, 40, supersample: 1);
        var flat = EyeScene.Of(FloorWorld(), Sprites(), flat: true).Draw(down, 40, 40, supersample: 1);

        var (texturedMean, texturedSpread) = Spread(textured);
        var (flatMean, flatSpread) = Spread(flat);
        await Assert.That(flatSpread).IsLessThan(1.0);
        await Assert.That(texturedSpread).IsGreaterThan(1000.0);
        await Assert.That(Math.Abs(texturedMean - flatMean)).IsLessThan(15.0);
    }

    [Test]
    public async Task A_texel_with_no_alpha_lets_the_ray_on_to_what_is_behind_it()
    {
        var world = FloorWorld();
        world.SetBlock(16, Floor + 1, 20, GoldBlock);
        world.SetBlock(16, Floor + 1, 16, Glass);

        var picture = EyeScene.Of(world, Sprites()).Draw(FacingTheGold, 64, 36);

        await Assert.That(Centre(picture)).IsEqualTo((160, 144, 0));
    }

    [Test]
    public async Task A_bottom_slab_fills_only_its_lower_half_so_the_eye_sees_over_it()
    {
        var world = FloorWorld();
        world.SetBlock(16, Floor + 1, 20, GoldBlock);
        world.SetBlock(16, Floor + 1, 18, StoneSlab);

        var picture = EyeScene.Of(world, Sprites()).Draw(FacingTheGold, 64, 36);

        await Assert.That(Centre(picture)).IsEqualTo((160, 144, 0));
        await Assert.That(picture.Seen.Select(seen => seen.Id)).Contains(StoneSlab);
    }

    [Test]
    public async Task A_top_slab_fills_its_upper_half_and_hides_what_is_behind_it()
    {
        var world = FloorWorld();
        world.SetBlock(16, Floor + 1, 20, GoldBlock);
        world.SetBlock(16, Floor + 1, 18, StoneSlab, 8);

        var picture = EyeScene.Of(world, Sprites()).Draw(FacingTheGold, 64, 36);

        await Assert.That(Centre(picture)).IsEqualTo((96, 96, 96));
    }

    [Test]
    public async Task A_fence_between_two_blocks_reaches_out_to_both_with_its_rails()
    {
        var beside = FacingTheGold with { X = 16.2, Pitch = 7.5 };
        var world = FloorWorld();
        world.SetBlock(16, Floor + 1, 20, GoldBlock);
        world.SetBlock(16, Floor + 1, 18, OakFence);
        var alone = EyeScene.Of(world, Sprites()).Draw(beside, 64, 36);
        world.SetBlock(15, Floor + 1, 18, Stone);
        world.SetBlock(17, Floor + 1, 18, Stone);

        var joined = EyeScene.Of(world, Sprites()).Draw(beside, 64, 36);

        await Assert.That(Centre(alone)).IsEqualTo((160, 144, 0));
        await Assert.That(Centre(joined)).IsEqualTo((120, 96, 56));
    }

    /// <summary>An eye level with a block standing four blocks south of it, looking straight at its north
    /// face.</summary>
    private static EyeCamera LevelWithTheLog => new(16.5, Floor + 1.5, 14.5, Yaw: 0, Pitch: 0, Fov: 40);

    private static (int Red, int Green, int Blue) Pixel(EyePicture picture, int row, int column)
    {
        var at = (row * picture.Width + column) * 3;
        return (picture.Rgb[at], picture.Rgb[at + 1], picture.Rgb[at + 2]);
    }

    [Test]
    public async Task A_log_lying_toward_the_eye_shows_its_sawn_end_and_one_standing_shows_bark()
    {
        var lying = FloorWorld();
        lying.SetBlock(16, Floor + 1, 18, OakLog, 8);
        var standing = FloorWorld();
        standing.SetBlock(16, Floor + 1, 18, OakLog);

        var end = EyeScene.Of(lying, Sprites()).Draw(LevelWithTheLog, 64, 36, supersample: 1);
        var bark = EyeScene.Of(standing, Sprites()).Draw(LevelWithTheLog, 64, 36, supersample: 1);

        await Assert.That(Centre(end)).IsEqualTo((144, 120, 80));
        await Assert.That(Centre(bark)).IsNotEqualTo((144, 120, 80));
    }

    /// <summary>The bark sprite here is dark on its left half and light on its right, so which way it varies
    /// across a face says which way the grain was turned: across a standing log's face, up a lying one's.</summary>
    [Test]
    public async Task The_bark_of_a_log_lying_across_the_eye_runs_along_the_log()
    {
        var world = FloorWorld();
        world.SetBlock(16, Floor + 1, 18, OakLog, 4);

        var picture = EyeScene.Of(world, Sprites()).Draw(LevelWithTheLog, 64, 36, supersample: 1);

        await Assert.That(Pixel(picture, 18, 26)).IsEqualTo(Pixel(picture, 18, 38));
        await Assert.That(Pixel(picture, 12, 32)).IsNotEqualTo(Pixel(picture, 24, 32));
    }

    [Test]
    public async Task A_camera_asked_to_frame_a_thing_stands_on_ground_and_sees_it()
    {
        var world = FloorWorld();
        for (var x = 23; x <= 25; x++)
            for (var y = Floor + 1; y <= Floor + 3; y++)
                for (var z = 23; z <= 25; z++)
                    world.SetBlock(x, y, z, Cobblestone);
        var scene = EyeScene.Of(world, Sprites());

        var camera = scene.Frame(24, 24);

        await Assert.That(camera).IsNotNull();
        await Assert.That(camera!.Value.Y).IsEqualTo(Floor + 2.62);
        var boulder = scene.Draw(camera.Value, 80, 45).Seen.FirstOrDefault(seen => seen.Id == Cobblestone);
        await Assert.That(boulder.Share).IsGreaterThan(0.02);
    }

    /// <summary>A gold block in a pit of cobblestone <paramref name="deep"/> blocks deep, ringed three blocks
    /// out.</summary>
    private static VoxelWorld Pit(int deep)
    {
        var world = FloorWorld();
        world.SetBlock(24, Floor + 1, 24, GoldBlock);
        for (var x = 21; x <= 27; x++)
            for (var z = 21; z <= 27; z++)
                if (Math.Max(Math.Abs(x - 24), Math.Abs(z - 24)) == 3)
                    for (var y = Floor + 1; y <= Floor + deep; y++)
                        world.SetBlock(x, y, z, Cobblestone);
        return world;
    }

    [Test]
    public async Task A_thing_no_ground_can_see_is_framed_from_the_air_over_it()
    {
        var scene = EyeScene.Of(Pit(deep: 8), Sprites());

        var camera = scene.Frame(24, 24);

        await Assert.That(camera).IsNotNull();
        await Assert.That(camera!.Value.Y).IsGreaterThan(Floor + 8);
        var gold = scene.Draw(camera.Value, 80, 45).Seen.FirstOrDefault(seen => seen.Id == GoldBlock);
        await Assert.That(gold.Share).IsGreaterThan(0.0);
    }

    [Test]
    public async Task A_thing_walled_in_higher_than_the_eye_rises_cannot_be_framed()
    {
        await Assert.That(EyeScene.Of(Pit(deep: 80), Sprites()).Frame(24, 24)).IsNull();
    }

    [Test]
    public async Task An_eye_facing_a_thing_from_over_the_void_hovers_level_with_its_middle()
    {
        var world = FloorWorld();
        for (var x = 23; x <= 25; x++)
            for (var y = Floor + 1; y <= Floor + 3; y++)
                for (var z = 23; z <= 25; z++)
                    world.SetBlock(x, y, z, Cobblestone);
        var scene = EyeScene.Of(world, Sprites());

        var hovering = scene.Facing(-12, 24, 24, 24);
        var raised = scene.Facing(-12, 24, 24, 24, eyeY: Floor + 20);

        await Assert.That(Math.Abs(hovering.Pitch)).IsLessThan(0.001);
        await Assert.That(raised.Y).IsEqualTo(Floor + 20.0);
        var boulder = scene.Draw(hovering, 80, 45).Seen.FirstOrDefault(seen => seen.Id == Cobblestone);
        await Assert.That(boulder.Share).IsGreaterThan(0.0);
    }

    [Test]
    public async Task A_block_with_no_sprite_is_drawn_in_its_palette_colour_and_counted()
    {
        var down = new EyeCamera(24.5, Floor + 6, 24.5, Yaw: 0, Pitch: 89, Fov: 40);

        var picture = EyeScene.Of(FloorWorld(), Sprites(withStone: false)).Draw(down, 20, 20, supersample: 1);

        await Assert.That(picture.Untextured).IsEqualTo(1.0);
        await Assert.That(picture.Sky).IsEqualTo(0.0);
    }

    private static (double Mean, double Variance) Spread(EyePicture picture)
    {
        var values = Enumerable.Range(0, picture.Width * picture.Height).Select(pixel => (double)picture.Rgb[pixel * 3]).ToList();
        var mean = values.Average();
        return (mean, values.Average(value => (value - mean) * (value - mean)));
    }
}
