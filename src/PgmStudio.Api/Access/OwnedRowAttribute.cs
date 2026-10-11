namespace PgmStudio.Api.Access;

/// <summary>
/// The route deletes a library row that belongs to whoever made it, so <see cref="AccessRules"/> gives it the
/// <c>member</c> policy and the endpoint asks the row itself (<c>SeededRows.RefusedAsync</c>): its owner or an
/// admin may delete it, and a built-in row nobody may.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class OwnedRowAttribute : Attribute;
