using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Serialization;
using FastEndpoints;
using PgmStudio.Domain;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// Refuse a request that left out a field the endpoint cannot do without, by name, before the endpoint runs.
///
/// <para><b>A record's nullability is a promise the runtime does not keep.</b> The request DTOs are positional
/// records of non-nullable types — <c>StyleSaveRequest(string Name, string Kind, string Params)</c> — and an
/// empty body binds every one of them to null anyway, because the annotation is a compile-time claim and the
/// binder is not bound by it. The endpoint then walked into it: seven of them answered a raw
/// <c>ArgumentNullException</c> from LINQ, and <c>POST /styles</c> carried the null all the way into MariaDB
/// and returned the database's own <c>Column 'name' cannot be null</c> to the caller.</para>
///
/// <para>So it is asked once, here, rather than in every handler. Anything the DTO declares non-nullable and
/// the request did not supply is a document that is wrong as posted — <c>RQ1</c>, 400, in the one envelope
/// <c>docs/refusals.md</c> describes, naming every missing field rather than the first, because an author
/// fixing one at a time is an author making four round trips.</para>
///
/// <para>Only the top level is read. A field's <i>contents</i> are the gate's business — a style whose blocks
/// are wrong is <c>HS1</c>'s to refuse, and this must not start having opinions about them.</para>
/// </summary>
internal sealed class RequiredFields : IGlobalPreProcessor
{
    /// <summary>Whether a property is required, by property. A <see cref="NullabilityInfoContext"/> caches
    /// into a dictionary of its own and is not thread-safe, so two concurrent requests reading one corrupt it;
    /// the answer is pure, so it is computed once per property against a fresh context and kept here.</summary>
    private static readonly ConcurrentDictionary<PropertyInfo, bool> RequiredByProperty = new();

    public async Task PreProcessAsync(IPreProcessorContext context, CancellationToken ct)
    {
        if (context.Request is not { } request) return;

        var missing = request.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanRead && Required(property) && Absent(property.GetValue(request)))
            .Select(OnTheWire)
            .ToList();
        if (missing.Count == 0) return;

        await Refusals.WriteAsync(
            context.HttpContext, 400, "incomplete request",
            missing.Select(field => new Finding(
                RequestRules.Unreadable,
                $"field '{field}' is required and was not supplied",
                Field: field)),
            ct);
    }

    /// <summary>Declared non-nullable, and of a kind a body actually carries. Value types are skipped: an
    /// <c>int</c> that was not sent is a zero and indistinguishable from one that was, so a default a caller
    /// meant cannot be told from a field they forgot, and refusing on it would refuse every legitimate
    /// request that leaned on a default.</summary>
    private static bool Required(PropertyInfo property) =>
        RequiredByProperty.GetOrAdd(property, static candidate =>
            !candidate.PropertyType.IsValueType
            && new NullabilityInfoContext().Create(candidate).ReadState is NullabilityState.NotNull);

    /// <summary>
    /// Null, and nothing else. A JSON body that omits a field binds it to null, so null <b>is</b> "not
    /// supplied" and the two need no separating.
    ///
    /// <para>Empty is a different thing and belongs to the gates. A theme binding no buckets is a theme with
    /// an empty list, not a theme missing one, and refusing it here would answer 400 where the request wants a
    /// 404. The same for a blank string: a style
    /// named <c>""</c> is a fault about the name's <i>value</i>, which is the kind of judgement the endpoint's
    /// own gate makes with a rule id that says what is wrong with it.</para>
    /// </summary>
    private static bool Absent(object? value) => value is null;

    /// <summary>The name the caller wrote, which is the only name they can look for. A property states its
    /// own where the two differ — <c>RegionId</c> crosses as <c>region_id</c> — and the rest follow the
    /// serializer's camelCase. Naming the property instead sends an author looking for a field their body
    /// does not have.</summary>
    private static string OnTheWire(PropertyInfo property) =>
        property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
        ?? (property.Name.Length == 0
            ? property.Name
            : char.ToLowerInvariant(property.Name[0]) + property.Name[1..]);
}
