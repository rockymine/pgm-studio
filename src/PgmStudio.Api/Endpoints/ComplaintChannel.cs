using Namotion.Reflection;
using NJsonSchema;
using NSwag;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// Publishes the channel <see cref="Complaints"/> writes: the <c>warnings</c> key a success may carry, and
/// the header that restates it.
///
/// <para>Every 2xx JSON object answers <c>warnings</c> when a gate remarked on something and carries no such
/// key when none did — one rule, over the whole surface, enforced by middleware rather than by the endpoint.
/// No record declares it and none should: adding a field to a hundred DTOs would restate one fact a hundred
/// times, and every one of them would then have to be filled. So the document says it here, once, in the
/// place the fact actually lives.</para>
///
/// <para>The shape is <c>allOf</c> — the answer the route names, plus the key — rather than a member added
/// to the referenced schema, because that schema is also a request body and a nested field elsewhere, and a
/// complaint rides on neither.</para>
///
/// <para><b>The rule is mechanical, not a judgement.</b> The key goes on a 2xx whose body is a JSON
/// <b>object</b>, which is exactly where <c>Complaints</c> can put one; the header goes on every 2xx,
/// because complaints are handed over before a response starts and nothing about a status or a media type
/// stops that. An answer that is a JSON <b>array</b> therefore names the header and not the key — the
/// twenty-one that are, are all plain list reads with no document to complain about, and one that grows a
/// gate would be reporting into a log rather than onto the wire.</para>
/// </summary>
internal sealed class ComplaintChannel : IOperationProcessor
{
    /// <summary>The key a success answers them under, and the header that restates it — the same two
    /// <see cref="Complaints"/> writes.</summary>
    private const string Key = "warnings";
    private const string Header = "Pgm-Warnings";

    public bool Process(OperationProcessorContext context)
    {
        foreach (var (status, response) in context.OperationDescription.Operation.Responses)
        {
            if (status.Length != 3 || status[0] != '2') continue;

            response.Headers[Header] = Restated();
            foreach (var (media, content) in response.Content)
            {
                if (!media.Contains("json", StringComparison.OrdinalIgnoreCase)) continue;
                if (content.Schema is not { } answered) continue;
                if (!IsObject(answered.ActualSchema)) continue;

                content.Schema = Extended(answered, context);
            }
        }
        return true;
    }

    /// <summary>Whether the answer is a JSON object — which is where <c>Complaints</c> can put the key.
    /// A record that inherits its fields states none of its own and composes the base with <c>allOf</c>
    /// instead, so the type has to be looked for through that rather than read off the top.</summary>
    private static bool IsObject(JsonSchema schema) =>
        schema.Type.HasFlag(JsonObjectType.Object)
        || schema.AllOf.Any(part => IsObject(part.ActualSchema));

    /// <summary>The answer the route names, plus the key it may carry.</summary>
    private static JsonSchema Extended(JsonSchema answered, OperationProcessorContext context)
    {
        var extended = new JsonSchema { Type = JsonObjectType.Object };
        extended.AllOf.Add(answered);
        extended.Properties[Key] = new JsonSchemaProperty
        {
            Type = JsonObjectType.Array,
            Item = context.SchemaGenerator.GenerateWithReference<JsonSchema>(
                typeof(Finding).ToContextualType(), context.SchemaResolver),
            Description =
                "What a gate remarked on without stopping the work. Present only when something was "
                + "remarked on, so an absent key means nothing was — a complaint the author may ignore, or a "
                + "decline saying one piece of what they wrote is not in what was built, told apart by each "
                + "finding's own severity. GET /api/rules explains any rule id one carries.",
        };
        return extended;
    }

    /// <summary>The header's own shape: the count, then each rule id once — what a caller reads where the
    /// body cannot hold the key, and a cheap read where it can.</summary>
    private static OpenApiHeader Restated() => new()
    {
        Schema = new JsonSchema { Type = JsonObjectType.String },
        Description =
            "Present only when the answer carries warnings: the count, then each rule id once "
            + "(\"3 DR-TREE RQ3\"). It is what a caller reads where the body cannot hold the key — a world "
            + "zip, a rendered map.xml — and a cheap read where it can.",
    };
}
