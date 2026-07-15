using System.Text.Json;
using PgmStudio.Api.Services;
using PgmStudio.Minecraft;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The plan editor's iso preview draws <see cref="PlanStructurePreview"/>'s boxes, so a box that misreports
/// where the world build puts a structure is a preview that lies. These compile the isolated-spawn seed both
/// ways — preview boxes and a real built world — and check every box against the blocks actually stamped: the
/// material fills the box, and the box is tight (the course past its top is something else). That pins the
/// per-structure coordinate conventions the boxes normalize (iron footprints are max-inclusive; room floors and
/// walls max-exclusive; a wall's TopY inclusive) and the claim that a marker's plan surface is the same Y the
/// build's per-column surface map resolves.
/// </summary>
public sealed class PlanStructurePreviewTests
{
    private static string SeedPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "tools", "seeds", "isolated-spawn.plan.json")))
            dir = dir.Parent;
        if (dir is null) throw new FileNotFoundException("isolated-spawn.plan.json not found above " + AppContext.BaseDirectory);
        return Path.Combine(dir.FullName, "tools", "seeds", "isolated-spawn.plan.json");
    }

    private static (IReadOnlyList<StructureBox> Boxes, VoxelWorld World) Build()
    {
        var plan = PlanModel.Parse(File.ReadAllText(SeedPath()))!;
        var boxes = PlanStructurePreview.Build(plan);
        var (layout, intent) = PlanCompiler.Compile(plan);
        var layoutJson = JsonSerializer.Serialize(layout, SketchLayout.Json);
        return (boxes, SketchWorldBuilder.Build(layoutJson, intent).World);
    }

    private static StructureBox First(IReadOnlyList<StructureBox> boxes, string kind)
        => boxes.First(b => b.Kind == kind);

    /// <summary>The box's four footprint corner columns (max is exclusive, so the far corner is max-1).</summary>
    private static (int X, int Z)[] Corners(StructureBox b) =>
        [(b.MinX, b.MinZ), (b.MaxX - 1, b.MinZ), (b.MinX, b.MaxZ - 1), (b.MaxX - 1, b.MaxZ - 1)];

    [Test]
    public async Task Every_structure_kind_is_previewed()
    {
        var (boxes, _) = Build();
        foreach (var kind in (string[])["spawn-cube", "wool-cage", "iron", "wall"])
            await Assert.That(boxes.Any(b => b.Kind == kind)).IsTrue();
    }

    [Test]
    public async Task Iron_box_is_filled_with_iron_and_is_tight()
    {
        var (boxes, w) = Build();
        var box = First(boxes, "iron");

        for (var x = box.MinX; x < box.MaxX; x++)
        for (var z = box.MinZ; z < box.MaxZ; z++)
        for (var y = box.Floor; y < box.Top; y++)
            await Assert.That(w.GetBlock(x, y, z).Id).IsEqualTo(Blocks.IronBlock);

        // Tight: the course above the box is not part of the cube.
        await Assert.That(w.GetBlock(box.MinX, box.Top, box.MinZ).Id).IsNotEqualTo(Blocks.IronBlock);
    }

    [Test]
    public async Task Wall_box_is_filled_with_bedrock_and_is_tight()
    {
        var (boxes, w) = Build();
        var box = First(boxes, "wall");

        for (var x = box.MinX; x < box.MaxX; x++)
        for (var z = box.MinZ; z < box.MaxZ; z++)
            await Assert.That(w.GetBlock(x, box.Top - 1, z).Id).IsEqualTo(Blocks.Bedrock);

        await Assert.That(w.GetBlock(box.MinX, box.Top, box.MinZ).Id).IsNotEqualTo(Blocks.Bedrock);
    }

    [Test]
    public async Task Cube_boxes_bound_the_stamped_shells()
    {
        var (boxes, w) = Build();

        foreach (var box in boxes.Where(b => b.Kind is "spawn-cube" or "wool-cage"))
        {
            // The floor course is bedrock at its corners (the centre is the 2×2 wool marker).
            foreach (var (x, z) in Corners(box))
                await Assert.That(w.GetBlock(x, box.Floor, z).Id).IsEqualTo(Blocks.Bedrock);

            // The shell's walls stand on the box's own perimeter — so the box is neither too big nor too small.
            foreach (var (x, z) in Corners(box))
                await Assert.That(w.GetBlock(x, box.Floor + 1, z).Id).IsEqualTo(Blocks.Bedrock);

            // Tight: nothing of the shell sits above the box's exclusive top.
            await Assert.That(w.GetBlock(box.MinX, box.Top, box.MinZ).Id).IsEqualTo(Blocks.Air);
        }
    }

    /// <summary>
    /// The symmetry orbit's whole promise: a structure and its mirror images are the same structure, so they
    /// must rest at the same height. They did not — a floor probed at the anchor picks the block on one side of
    /// a grid line, and the mirror of block <c>b</c> is <c>-1-b</c>, so the images read different columns; where
    /// a marker sat at a terrain edge one image found ground at y=13 and the other fell to the y=1 fallback, in
    /// the void. Probing the footprint (which is its own mirror) makes every image agree.
    /// </summary>
    [Test]
    public async Task Orbit_images_of_a_structure_all_rest_at_the_same_height()
    {
        var (boxes, _) = Build();

        // Group each kind's boxes by size — one group per structure, its members the orbit images.
        foreach (var group in boxes.GroupBy(b => (b.Kind, W: b.MaxX - b.MinX, D: b.MaxZ - b.MinZ, H: b.Top - b.Floor)))
        {
            var floors = group.Select(b => b.Floor).Distinct().ToList();
            await Assert.That(floors.Count).IsEqualTo(1);
        }

        // And none of them fell back to the bottom of the world.
        foreach (var box in boxes.Where(b => b.Kind != "wall"))
            await Assert.That(box.Floor).IsGreaterThan(1);
    }

    [Test]
    public async Task Cubes_carry_their_team_and_wool_colours()
    {
        var (boxes, _) = Build();
        foreach (var box in boxes.Where(b => b.Kind is "spawn-cube" or "wool-cage"))
            await Assert.That(string.IsNullOrEmpty(box.Color)).IsFalse();

        // Iron and walls have a fixed material, so they name no colour.
        await Assert.That(First(boxes, "iron").Color).IsNull();
        await Assert.That(First(boxes, "wall").Color).IsNull();
    }
}
