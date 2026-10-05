using PgmStudio.Vocabulary;
namespace PgmStudio.Domain;

/// <summary>
/// What a shop cannot be, beyond what its own elements say. The menu's shape is PGM's and the parser holds
/// it; what is left is the <b>references</b> a shop makes to the rest of the document, which nothing inside a
/// <c>&lt;shops&gt;</c> block can check for itself.
/// </summary>
public static class ShopRules
{
    /// <summary>A shopkeeper names a shop or a region, or a shop item names an action, that the map does not
    /// have.</summary>
    /// <remarks>Either change the <c>shop</c> or <c>region</c> of the shopkeeper to one the map has, or delete the
    /// <c>action</c> from <c>items</c>.</remarks>
    [Rule(RuleCategory.Unplayable, RuleConcern.Intent, RuleConcern.Studio)]
    public const string ReferenceNotDefined = "SH1";
}
