using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>
/// The shape catalog (G144) — the vocabulary as data the studio renders. What is gated here is the property
/// the page exists for: that a card's <see cref="CatalogTier"/> matches what the pipeline really does with
/// that shape. A catalog whose tiers drift is worse than no catalog, because it asserts a vocabulary the
/// boards do not carry.
/// </summary>
public sealed class ShapeCatalogTests
{
    private static IReadOnlyList<CatalogShape> Catalog => ShapeCatalog.All();

    private static IEnumerable<CatalogShape> Wools => Catalog.Where(s => s.Kind == "wool");

    [Test]
    public async Task Catalog_is_non_empty_and_every_card_carries_a_renderable_plan()
    {
        await Assert.That(Catalog).IsNotEmpty();
        foreach (var shape in Catalog)
        {
            await Assert.That(shape.Plan.Pieces).IsNotEmpty();
            await Assert.That(shape.Plan.Globals.Symmetry).IsEqualTo("none");
            await Assert.That(shape.BoxW).IsGreaterThan(0);
            await Assert.That(shape.BoxH).IsGreaterThan(0);
        }
    }

    [Test]
    public async Task Ids_are_unique_so_a_card_can_key_on_them()
    {
        await Assert.That(Catalog.Select(s => s.Id).Distinct().Count()).IsEqualTo(Catalog.Count);
    }

    [Test]
    public async Task Every_in_mix_family_is_one_the_production_menu_admits()
    {
        // the mix is drawn from the sampler, so it can only ever be a subset of what the fill menu allows
        var inMix = Wools.Where(s => s.Tier == CatalogTier.InMix).Select(s => s.Family).Distinct();
        var menu = FillMenu.ProductionFamilies.Select(f => f.ToString().ToLowerInvariant()).ToList();
        foreach (var family in inMix) await Assert.That(menu).Contains(family);
    }

    [Test]
    public async Task The_families_the_menu_excludes_are_emitter_only()
    {
        // the scythe is off ProductionFamilies (WL8 — a flush dock seals its bay), so it can never be in the mix
        var scythe = Wools.Where(s => s.Family == "scythe").ToList();
        await Assert.That(scythe).IsNotEmpty();
        foreach (var shape in scythe) await Assert.That(shape.Tier).IsEqualTo(CatalogTier.EmitterOnly);
    }

    [Test]
    public async Task Z_is_on_the_menu_but_no_sampler_draws_it_so_it_reads_reachable()
    {
        // the honesty case the page exists for: the menu advertises Z, the sampler never asks (G146)
        await Assert.That(FillMenu.ProductionFamilies).Contains(ShapeFamily.Z);
        var z = Wools.Where(s => s.Family == "z").ToList();
        await Assert.That(z).IsNotEmpty();
        foreach (var shape in z) await Assert.That(shape.Tier).IsEqualTo(CatalogTier.Reachable);
    }

    [Test]
    public async Task Every_non_in_mix_card_explains_itself()
    {
        // a bare "reachable" badge tells an author nothing — the note is the whole point of the tier
        foreach (var shape in Catalog.Where(s => s.Tier != CatalogTier.InMix))
            await Assert.That(string.IsNullOrWhiteSpace(shape.Note)).IsFalse();
    }

    [Test]
    public async Task The_mix_contains_the_families_the_sampler_actually_rolls()
    {
        // UnitRequests.WoolRequest rolls donut / U / H / clamp / L and falls back to I — all six must appear,
        // or the sweep that collects them has stopped reaching the sampler's branches
        var inMix = Wools.Where(s => s.Tier == CatalogTier.InMix).Select(s => s.Family).Distinct().ToList();
        foreach (var family in new[] { "i", "l", "u", "h", "clamp", "donut" })
            await Assert.That(inMix).Contains(family);
    }

    [Test]
    public async Task The_donut_dominates_the_in_mix_variety()
    {
        // the measured sampling imbalance (G144/G118): the donut's sampled hole and entry width give it most
        // of the shape variety, while U/H/L are one shape apiece. This is a fact the verdict corpus inherits.
        var inMix = Wools.Where(s => s.Tier == CatalogTier.InMix).ToList();
        var donuts = inMix.Count(s => s.Family == "donut");
        await Assert.That(donuts).IsGreaterThan(inMix.Count / 2);
    }

    [Test]
    public async Task Unplumbed_knobs_are_emitter_only_and_name_the_knob()
    {
        // the five knobs WoolBoxEmitter.Fill drops (G145) — each drawn once, labelled, never claimed in-mix
        var knobbed = Wools.Where(s => s.Knobs.Any(k => k.Contains("attach") || k.Contains("shift")
                                                        || k.Contains("extend"))).ToList();
        foreach (var shape in knobbed.Where(s => s.Tier == CatalogTier.EmitterOnly))
            await Assert.That(shape.Knobs).IsNotEmpty();
        await Assert.That(Wools.Any(s => s.Tier == CatalogTier.EmitterOnly && s.Knobs.Count > 0)).IsTrue();
    }

    [Test]
    public async Task A_cards_box_is_what_the_shape_needs_not_a_display_size()
    {
        // The box is printed on the card, so a generous one reads as a requirement. These knobs are nearly
        // free — the donut's second attachment costs no extra box at all at w2 — so anything approaching
        // twice the family's own minimum means a display size was hard-coded again rather than derived.
        foreach (var shape in Wools.Where(s => s.Tier == CatalogTier.EmitterOnly))
        {
            var family = Enum.Parse<ShapeFamily>(shape.Family, ignoreCase: true);
            var (minW, minH) = ShapeEmitter.MinBox(family, shape.CorridorCells);
            await Assert.That(shape.BoxW).IsGreaterThanOrEqualTo(minW);
            await Assert.That(shape.BoxH).IsGreaterThanOrEqualTo(minH);
            await Assert.That(shape.BoxW * shape.BoxH).IsLessThan(2 * minW * minH);
        }
    }

    [Test]
    public async Task Bodies_cover_both_form_menus_and_are_all_in_the_mix()
    {
        var hubs = Catalog.Where(s => s.Kind == "hub").ToList();
        var fronts = Catalog.Where(s => s.Kind == "frontline").ToList();
        await Assert.That(hubs).IsNotEmpty();
        await Assert.That(fronts).IsNotEmpty();
        // the allocator samples both menus, so a body form is never anything but in-mix
        foreach (var shape in hubs.Concat(fronts)) await Assert.That(shape.Tier).IsEqualTo(CatalogTier.InMix);
    }

    [Test]
    public async Task No_two_cards_draw_the_same_cells()
    {
        // the catalog deduplicates by cell pattern, so a family knob that changes nothing never gets a card
        var wools = Wools.ToList();
        var patterns = wools.Select(s => string.Join(
            ";", s.Plan.Pieces.OrderBy(p => p.Rect.Z).ThenBy(p => p.Rect.X)
                .Select(p => $"{p.Rect.X},{p.Rect.Z},{p.Rect.Width},{p.Rect.Height},{p.Role}")));
        await Assert.That(patterns.Distinct().Count()).IsEqualTo(wools.Count);
    }
}
