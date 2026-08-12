#:project ../../src/PgmStudio.Geom/PgmStudio.Geom.csproj
// Is a branch tip inside the leaf cluster that is supposed to hang on it?
using PgmStudio.Geom;
using PgmStudio.Geom.Algorithms;

static double Quadric(LeafCluster c, Vec3 p)
{
    var d = p - c.Center;
    return d.X * d.X / (c.Radii.X * c.Radii.X) + d.Y * d.Y / (c.Radii.Y * c.Radii.Y)
         + d.Z * d.Z / (c.Radii.Z * c.Radii.Z);
}

Console.WriteLine($"{"height",7} {"leafSize",9} {"clusters",9} {"tip outside",12} {"median gap",11} {"vert lift",10} {"radius",7} {"halfheight",11}");
foreach (var height in new double[] { 6, 9, 12, 16, 20, 26, 32, 40 })
    foreach (var leafSize in new double[] { 0.2, 0.6, 1.0 })
    {
        int total = 0, outside = 0;
        var gaps = new List<double>();
        var lifts = new List<double>();
        var radii = new List<double>();
        var heights = new List<double>();
        for (uint seed = 1; seed <= 12; seed++)
        {
            var shape = new TreeShape(Height: height, Stems: 1, Levels: 2,
                BranchAngle: 0.55, Flow: 0.45, Leader: 0.55);
            var grown = TreeSkeleton.Grow(shape, seed);
            var clusters = TreeCrown.Clusters(grown.Tips, leafSize, shape.Size, seed);
            for (var i = 0; i < clusters.Count; i++)
            {
                total++;
                var quadric = Quadric(clusters[i], grown.Tips[i].Position);
                if (quadric > 1) outside++;
                gaps.Add(Math.Sqrt(quadric));
                lifts.Add(clusters[i].Center.Y - grown.Tips[i].Position.Y);
                radii.Add(clusters[i].Radii.X);
                heights.Add(clusters[i].Radii.Y);
            }
        }
        gaps.Sort(); lifts.Sort(); radii.Sort(); heights.Sort();
        Console.WriteLine($"{height,7} {leafSize,9:0.0} {total,9} {outside * 100.0 / total,11:0.0}% " +
            $"{gaps[gaps.Count / 2],11:0.00} {lifts[lifts.Count / 2],10:0.0} {radii[radii.Count / 2],7:0.0} {heights[heights.Count / 2],11:0.0}");
    }
