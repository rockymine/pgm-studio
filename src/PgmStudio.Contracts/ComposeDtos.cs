namespace PgmStudio.Contracts;

/// <summary>The wire form of a generated plan's canonical versioned request descriptor — a browse card's
/// identity. Reproducible within a composer version: the server re-composes the exact plan from these fields
/// to pin or open it, so a card never has to carry its plan JSON.</summary>
/// <param name="Players">Players per team, 5–32 — the range the seed envelopes are calibrated over. It is
/// what drives the board's size.</param>
/// <param name="Teams">2 or 4.</param>
/// <param name="Symmetry">The board's symmetry: <c>rot_180</c>, <c>mirror_x</c> or <c>mirror_z</c> for two
/// teams, and <c>rot_90</c> for four, which take no other.</param>
/// <param name="Cell">Blocks per proxy cell — the plan grid's scale.</param>
/// <param name="Seed">Drives every random draw the composer makes, so the same seed under the same composer
/// version reproduces the same board.</param>
/// <param name="ComposerVersion">Which composer built it. A card composed by an older one reproduces a
/// different board from the same seed, which is what <c>staleComposer</c> on a plan row says.</param>
/// <param name="Schema">The descriptor's own shape version, so a stored descriptor written before these
/// fields changed still reads.</param>
public sealed record ComposeRequestDto(
    int Players,
    int Teams,
    string Symmetry,
    int Cell,
    ulong Seed,
    string ComposerVersion,
    int Schema);

/// <summary>One fired soft term's contribution to a board's score (weight × distance), for the detail
/// breakdown.</summary>
/// <param name="TermId">Which term fired — the metric id a profile keys on.</param>
/// <param name="RuleId">The <c>rules.md</c> id it scores, which is what to ask <c>GET /api/rules</c>
/// about.</param>
/// <param name="Contribution">Weight × distance: how much of the board's score this term is.</param>
public sealed record TermContribDto(string TermId, string RuleId, double Contribution);

/// <summary>A board's structural read — the sieve/bucket vocabulary as display tokens: the sorted wool
/// approach families, the hub body form, and the frontline form (<c>none</c> when the unit has no frontline).
/// The tokens double as the card badges and the filter values.</summary>
/// <param name="Wools">The wool approach families the unit built, sorted.</param>
/// <param name="Hub">The hub body's form.</param>
/// <param name="Frontline">The frontline's form, or <c>none</c> where the unit has no frontline.</param>
public sealed record StructureSummaryDto(
    IReadOnlyList<string> Wools,
    string Hub,
    string Frontline);

/// <summary>What one kind of box spent, summed over every box of that kind in the team unit.
/// <paramref name="Boxes"/> is how many there were (two wools read as one row of two).
///
/// <para>The two numbers are different currencies and both are needed. <paramref name="FootprintCells"/> is
/// the box rectangle — fixed when the box is seated; <paramref name="LandCells"/> is the walkable terrain
/// inside it — what the fill actually spends. A donut has a large footprint and modest land, because the
/// enclosed hole is footprint it never spends, so either number alone misreads the shape.</para></summary>
/// <param name="Kind">Which kind of box — a spawn, a wool approach, the hub, the frontline, the mid.</param>
/// <param name="Boxes">How many there were (two wools read as one row of two).</param>
/// <param name="LandCells">The walkable terrain inside them, which is what the fill actually spends.</param>
/// <param name="FootprintCells">The box rectangles, which are fixed once the boxes are seated.</param>
public sealed record BoxSpendDto(string Kind, int Boxes, int LandCells, int FootprintCells);

/// <summary>What a composed unit spent against the budget it was built to. <paramref name="BudgetCells"/> is
/// the envelope's per-team land target converted to cells — <b>per team unit</b>, not per board, because the
/// board is the unit fanned; and converted from the blocks² the envelope works in, which is why it is carried
/// already-converted rather than leaving the client to guess the cell size. <paramref name="ByKind"/> is the
/// breakdown, ordered largest-land first.</summary>
/// <param name="LandCells">The walkable terrain the unit spent in total.</param>
/// <param name="FootprintCells">The rectangles it seated in total.</param>
/// <param name="BudgetCells">The envelope's land target for one team unit, already converted from the
/// blocks² the envelope works in — carried converted rather than leaving a caller to guess the cell
/// size.</param>
/// <param name="ByKind">The breakdown, largest-land first.</param>
public sealed record LandSpendDto(
    int LandCells,
    int FootprintCells,
    double BudgetCells,
    IReadOnlyList<BoxSpendDto> ByKind);

/// <summary>One card in the browse feed: its <paramref name="Descriptor"/> (identity + reproduction key), the
/// evaluator <paramref name="Score"/> (lower is better), the base-unit <paramref name="WoolCount"/>, its
/// <paramref name="Structure"/> read (families/forms, for badges + filtering), any fired hard-term ids, the
/// top soft contributors, what it <paramref name="Spend"/>t against its land budget, and the ready-to-inject
/// board <paramref name="Svg"/>.</summary>
/// <param name="Descriptor">The card's identity, and the key that reproduces its board.</param>
/// <param name="Score">The evaluator's verdict, lower being better.</param>
/// <param name="WoolCount">How many wools the base team unit carries.</param>
/// <param name="Structure">What the board turned out to be, as the badge and filter tokens.</param>
/// <param name="HardTerms">The hard terms it fired, by id. A card firing any is one the gate rejected.</param>
/// <param name="TopSoft">The soft terms costing it the most, largest first.</param>
/// <param name="Svg">The board picture, ready to inject.</param>
/// <param name="Spend">What the unit spent against its land budget, absent where nothing measured it.</param>
public sealed record ComposeCard(
    ComposeRequestDto Descriptor,
    double Score,
    int WoolCount,
    StructureSummaryDto Structure,
    IReadOnlyList<string> HardTerms,
    IReadOnlyList<TermContribDto> TopSoft,
    string Svg,
    LandSpendDto? Spend = null);

/// <summary>What the structural vocabulary actually turned up over the boards a page composed, counted
/// <b>before</b> the sieve so a filter never hides the alternatives it is filtering against.
/// <paramref name="Boards"/> is how many boards the counts are drawn from (composed, not matched).
///
/// <para>This is what makes a filter chip say something. Which forms a request can produce is a property of the
/// <em>request</em>, not a constant — it rides on the box sizes the land budget buys, so a small board never
/// reaches the wide hub forms at all. Rather than predict that, the feed reports what it saw, and a token
/// missing from a large enough sample is the honest version of "this request does not make one".</para></summary>
/// <param name="Boards">How many boards the counts are drawn from — composed, not matched.</param>
/// <param name="Wools">How often each wool approach family turned up.</param>
/// <param name="Hubs">How often each hub form turned up.</param>
/// <param name="Frontlines">How often each frontline form turned up.</param>
public sealed record ObservedForms(
    int Boards,
    IReadOnlyDictionary<string, int> Wools,
    IReadOnlyDictionary<string, int> Hubs,
    IReadOnlyDictionary<string, int> Frontlines);

/// <summary>A page of browse cards. <paramref name="NextSeed"/> is the seed cursor to resume from (feed
/// forward for infinite scroll); <paramref name="Exhausted"/> is true when the per-request scan budget was
/// reached before filling the page, so the client can stop requesting; <paramref name="Scanned"/> is how many
/// seeds this page composed (matched = Cards.Count) — under a strict structural filter the low match rate is
/// itself the signal to promote that filter to a held target. <paramref name="Observed"/> is the structural
/// census over those same boards, which the filter chips read to say what this request produces.</summary>
/// <param name="Cards">The boards this page matched.</param>
/// <param name="NextSeed">The seed cursor to resume from, for a feed that scrolls forward.</param>
/// <param name="Exhausted">Whether the scan budget ran out before the page filled, so a client stops
/// asking.</param>
/// <param name="Scanned">How many seeds this page composed. Against the card count it is the match rate,
/// and a low one under a strict filter is itself the signal to promote that filter to a held target.</param>
/// <param name="Observed">The structural census over those same boards, counted <b>before</b> the sieve so
/// a filter chip never hides the alternatives it is filtering against.</param>
public sealed record ComposePage(
    IReadOnlyList<ComposeCard> Cards,
    int NextSeed,
    bool Exhausted,
    int Scanned,
    ObservedForms? Observed = null);
