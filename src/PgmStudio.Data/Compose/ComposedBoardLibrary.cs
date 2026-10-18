using System.Diagnostics;
using System.Text.Json;
using PgmStudio.Contracts;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Geom;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Derive;
using PgmStudio.Pgm.Evaluate;
using PgmStudio.Pgm.Render;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Vocabulary;

namespace PgmStudio.Data.Compose;

/// <summary>The rest of a library board's card, kept as its <c>card_json</c>.</summary>
internal sealed record ComposedBoardExtras(
    IReadOnlyList<string> HardTerms, IReadOnlyList<TermContribDto> TopSoft, LandSpendDto Spend);

/// <summary>A library board as a map is made from it: its layout, its intent labelled for a player count, and its
/// boxes as the map's places.</summary>
public sealed record ComposedBoardSketch(string Name, SketchLayout Layout, MapIntent Intent, PlacesDocument Places);

/// <summary>
/// The composed-board library the Generator page browses: <see cref="PerBand"/> boards for every size band and
/// symmetry the feed offers, composed once per composer version, scored and stored with their cards, so a
/// browse is a read and nothing is composed on request (<c>docs/tools/generator.md</c>).
///
/// <para>A board depends on the band rather than the player count: across one band's counts the same seed
/// composes the same board, and only its label differs (<see cref="Composer.Label"/>). So a board is stored
/// once per band, composed at the band's lowest count, and labelled with a caller's own count when it is shown
/// or made into a map.</para>
///
/// <para>A board is stored as the sketch it is handed over as (<see cref="ComposedSketch"/>) — one shape per box,
/// the intent its markers compile to, and its boxes as places — beside the picture of the pieces the composer laid,
/// which keeps each piece's role so a page can tint one.</para>
/// </summary>
public static class ComposedBoardLibrary
{
    /// <summary>How many boards the library holds for each size band and symmetry.</summary>
    public const int PerBand = 500;

    /// <summary>The symmetries the feed offers.</summary>
    public static readonly string[] Symmetries = ["rot_180", "mirror_z"];

    /// <summary>The team count the feed composes for.</summary>
    public const int Teams = 2;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>A wool family as the <c>wools</c> column holds it, between commas, so asking for one cannot match
    /// another whose name contains it.</summary>
    public static string WoolToken(string family) => $",{family},";

    /// <summary>The library board for <paramref name="band"/>, <paramref name="symmetry"/> and
    /// <paramref name="seed"/>, or null where that seed composes nothing.</summary>
    public static ComposedBoardRow? Compose(string band, string symmetry, ulong seed)
    {
        var request = new ComposeRequest(SizeBands.Players(band).Low, Teams, symmetry, seed, ComposeRequest.DefaultCell);
        ComposedStages stages;
        try { stages = Composer.ComposeStages(request); }
        catch (ComposeException) { return null; }

        var profile = EvaluationProfile.Composer;
        var evaluation = LayoutEvaluator.Evaluate(stages.Plan, profile);
        var summary = StructureSummary.Derive(stages.Unit);
        var extras = new ComposedBoardExtras(
            [.. evaluation.Terms.Where(term => term.Kind == TermKind.Hard && term.Violation is not null).Select(term => term.TermId)],
            [
                .. evaluation.Terms
                    .Where(term => term.Kind == TermKind.Soft && term.Distance > 0)
                    .Select(term => new TermContribDto(term.TermId, term.Violation?.RuleId ?? "", profile.Weight(term.TermId) * term.Distance))
                    .OrderByDescending(term => term.Contribution).Take(3),
            ],
            Spend(stages));
        var sketch = ComposedSketch.Of(stages);

        return new ComposedBoardRow
        {
            ComposerVersion = ComposerVersion.Current,
            Band = band,
            Symmetry = symmetry,
            Cell = request.Cell,
            Seed = seed,
            Score = evaluation.Score,
            WoolCount = stages.Plan.Placements.Wools.Count,
            Wools = "," + string.Concat(summary.Wools.Select(family => StructureNames.Family(family) + ",")),
            Hub = StructureNames.Form(summary.Hub),
            Frontline = StructureNames.Form(summary.Frontline),
            CardJson = JsonSerializer.Serialize(extras, Json),
            LayoutJson = JsonSerializer.Serialize(sketch.Layout, SketchLayout.Json),
            IntentJson = JsonSerializer.Serialize(sketch.Intent, MapArtifactStore.Json),
            PlacesJson = JsonSerializer.Serialize(PlacesOf(sketch), MapArtifactStore.Json),
            Svg = PlanBoardSvg.Render(stages.Plan),
            CreatedAt = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Compose what the current composer version is missing, up to <paramref name="perBand"/> boards for each of
    /// <paramref name="bands"/> and <paramref name="symmetries"/>, one board at a time from seed 0, skipping a
    /// seed already stored or composing nothing. Once every band and symmetry the feed offers holds
    /// <see cref="PerBand"/>, the boards other versions made are deleted. Returns how many it composed.
    /// </summary>
    public static async Task<int> FillAsync(
        ComposedBoardStore store, int perBand, IReadOnlyList<string> bands, IReadOnlyList<string> symmetries,
        Action<string> log, CancellationToken ct = default)
    {
        var composed = 0;
        foreach (var band in bands)
        foreach (var symmetry in symmetries)
        {
            var held = await store.SeedsAsync(ComposerVersion.Current, band, symmetry, ComposeRequest.DefaultCell, ct);
            var clock = Stopwatch.StartNew();
            var added = 0;
            // A seed that composes nothing is skipped; the bound keeps a composer that stops composing from
            // looping for ever.
            for (ulong seed = 0; held.Count < perBand && seed < (ulong)perBand * 4; seed++)
            {
                ct.ThrowIfCancellationRequested();
                if (held.Contains(seed) || Compose(band, symmetry, seed) is not { } row) continue;
                await store.InsertAsync(row, ct);
                held.Add(seed);
                added++;
            }
            composed += added;
            log($"{band} {symmetry}: {held.Count} of {perBand} ({added} composed in {clock.Elapsed.TotalSeconds:F0} s)");
        }

        var complete = true;
        foreach (var band in SizeBands.All)
        foreach (var symmetry in Symmetries)
            complete &= (await store.SeedsAsync(ComposerVersion.Current, band, symmetry, ComposeRequest.DefaultCell, ct)).Count >= PerBand;
        if (complete && await store.DeleteOtherVersionsAsync(ComposerVersion.Current, ct) is > 0 and var dropped)
            log($"deleted {dropped} boards other composer versions made");
        return composed;
    }

    /// <summary>The card for <paramref name="row"/>, labelled for <paramref name="players"/> per team.</summary>
    public static ComposeCard CardOf(ComposedBoardRow row, int players)
    {
        var extras = JsonSerializer.Deserialize<ComposedBoardExtras>(row.CardJson, Json)!;
        return new ComposeCard(
            new ComposeRequestDto(players, Teams, row.Symmetry, row.Cell, row.Seed, row.ComposerVersion, ComposeDescriptor.CurrentSchema),
            row.Score, row.WoolCount,
            new StructureSummaryDto(Families(row.Wools), row.Hub, row.Frontline),
            extras.HardTerms, extras.TopSoft, row.Svg, extras.Spend);
    }

    /// <summary>The sketch of <paramref name="row"/> a map is made from, labelled for <paramref name="players"/>
    /// per team.</summary>
    public static ComposedBoardSketch SketchOf(ComposedBoardRow row, int players)
    {
        var intent = Composer.Label(JsonSerializer.Deserialize<MapIntent>(row.IntentJson, MapArtifactStore.Json)!,
            players, Teams, row.Seed);
        return new ComposedBoardSketch(intent.Meta!.Name,
            JsonSerializer.Deserialize<SketchLayout>(row.LayoutJson, SketchLayout.Json)!, intent,
            JsonSerializer.Deserialize<PlacesDocument>(row.PlacesJson, MapArtifactStore.Json)!);
    }

    /// <summary>A composed board's boxes as places: one box-shaped place per box at every orbit image, named for the
    /// team that image belongs to and holding nothing.</summary>
    private static PlacesDocument PlacesOf(ComposedSketch sketch) => new(
    [
        .. sketch.Boxes.Select(box =>
        {
            var team = sketch.Intent.Teams![box.Image];
            return new Place($"{team.Id}-{box.Id}", $"{team.Name} {Called(box)}", PlaceShapes.Box,
                [[box.Rect.MinX, box.Rect.MinZ], [box.Rect.MaxX, box.Rect.MaxZ]]);
        }),
    ]);

    /// <summary>What a box is called among its team's places: <c>hub</c>, <c>spawn</c>, <c>front line</c>, and a
    /// wool approach by the letter its id ends in.</summary>
    private static string Called(ComposedBox box) => box.Kind switch
    {
        BoxKind.Hub => "hub",
        BoxKind.Spawn => "spawn",
        BoxKind.Frontline => "front line",
        BoxKind.Wool => $"wool {box.Id[(box.Id.LastIndexOf('-') + 1)..].ToUpperInvariant()}",
        _ => box.Id,
    };

    /// <summary>How often each wool family, hub form and frontline form appears across <paramref name="forms"/>.
    /// A family counts once per board however many approaches of it the board has, so every count reads against
    /// the same number of boards.</summary>
    public static ObservedForms Census(IReadOnlyList<(string Wools, string Hub, string Frontline)> forms)
    {
        Dictionary<string, int> wools = [], hubs = [], fronts = [];
        foreach (var (familiesHeld, hub, front) in forms)
        {
            foreach (var family in Families(familiesHeld).Distinct()) wools[family] = wools.GetValueOrDefault(family) + 1;
            hubs[hub] = hubs.GetValueOrDefault(hub) + 1;
            fronts[front] = fronts.GetValueOrDefault(front) + 1;
        }
        return new ObservedForms(forms.Count, wools, hubs, fronts);
    }

    /// <summary>The wire form of a composed request's descriptor.</summary>
    public static ComposeRequestDto DtoOf(ComposeDescriptor descriptor) =>
        new(descriptor.PlayersPerTeam, descriptor.Teams, descriptor.Symmetry, descriptor.Cell, descriptor.Seed,
            descriptor.ComposerVersion, descriptor.Schema);

    private static List<string> Families(string held) =>
        [.. held.Split(',', StringSplitOptions.RemoveEmptyEntries)];

    /// <summary>What the unit spent, from the partition the box pipeline produced. The per-kind rows are box
    /// footprints; the unit's <b>land</b> is read off the filled pieces, the same reading the spend gate takes.
    /// The budget is the envelope's own, converted from blocks² to cells here so the client never has to know
    /// the cell size.</summary>
    private static LandSpendDto Spend(ComposedStages stages)
    {
        var byKind = BoxLayout.Of(stages.Unit).Boxes
            .GroupBy(box => box.Kind)
            .Select(group => new BoxSpendDto(
                group.Key.ToString().ToLowerInvariant(),
                group.Count(),
                group.Sum(box => box.LandTargetCells),
                group.Sum(box => box.Rect.Width * box.Rect.Height)))
            .OrderByDescending(kind => kind.LandCells)
            .ToList();
        return new LandSpendDto(
            stages.Envelope.Band,
            new LandAgainstBudgetDto(Composer.LandCells(stages.Unit), stages.Envelope.UnitBudgetCells),
            new LandAgainstBudgetDto(
                MidCarver.StoneLandCells(stages.Envelope, stages.Mid.Stones), stages.Envelope.MidLandCells),
            byKind.Sum(kind => kind.FootprintCells),
            byKind);
    }
}
