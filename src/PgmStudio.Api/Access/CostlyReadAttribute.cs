namespace PgmStudio.Api.Access;

/// <summary>
/// The route builds what it answers on request and stores nothing: a view computed from a posted document, or a
/// map's world export. It is a read, but not a free one, so <see cref="AccessRules"/> gives it the
/// <c>member</c> policy whichever verb it takes and whichever map it names, rather than opening it to anyone.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CostlyReadAttribute : Attribute;
