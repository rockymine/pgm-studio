namespace PgmStudio.Api.Access;

/// <summary>
/// The route answers a view computed from the document it is posted, and stores nothing — a read that carries
/// a body, which is the only reason it is a <c>POST</c>. <see cref="AccessRules"/> opens it to anyone signed in,
/// whichever map it names, since looking at a map is not changing it. It stays closed to a signed-out caller:
/// such a view is built on request, and a build is not free.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class PostedReadAttribute : Attribute;
