using PgmStudio.Vocabulary;
namespace PgmStudio.Domain;

/// <summary>
/// What a shop cannot be, beyond what its own elements say. The menu's shape is PGM's and the parser holds
/// it; what is left is the <b>references</b> a shop makes to the rest of the document, which nothing inside a
/// <c>&lt;shops&gt;</c> block can check for itself.
/// </summary>
public static class ShopRules
{
    /// <summary>A shop reference names something the document does not define, so PGM refuses the whole map at
    /// load rather than opening a menu without it. Three references are read: a keeper's <c>shop</c>, which
    /// throws <i>"No shop with id '…' could be found"</i>; a keeper's <c>region</c>, and an icon's
    /// <c>action</c>, which are feature references and throw when nothing resolves them.</summary>
    /// <remarks>Define what is named, or stop naming it. A menu comes from the intent's own `shops`, so a keeper can only open one the same intent states; a region has to be one the map already holds; and an action or a kit is a feature the studio does not author at all, so an id there names nothing on a studio-built board. An imported map is exempt: a map may take its menus from an `&lt;include&gt;` this parser reads without splicing, which is what fifteen of the corpus's shop boards do.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Intent, RuleConcern.Studio)]
    public const string ReferenceNotDefined = "SH1";
}
