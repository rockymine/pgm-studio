using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using PgmStudio.Contracts;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// What a route answers, in the form the generated document publishes it.
///
/// <para>Most of the surface answers one JSON shape and says so by naming it as the endpoint's response type,
/// which the generator reads without being told. These are the rest: a route whose answer is <b>not</b> a JSON
/// document, or is not only one. A path and a verb say nothing about whether a caller is about to receive a
/// picture, a grid of characters or a world, and that is the one thing a schema exists to remove — so the
/// media type is declared here rather than discovered by sending the request.</para>
///
/// <para>One method per media type the studio serves, so the set is countable and a new one is a deliberate
/// addition rather than a string typed at a call site.</para>
/// </summary>
internal static class Answers
{
    /// <summary>The route draws a picture on <c>?format=png</c>, beside the SVG-in-JSON it answers by
    /// default — the form an agent saves and looks at. Six preview routes offer it; see
    /// <see cref="PngAnswer"/> for the parameters and the refusal.
    ///
    /// <para><paramref name="views"/> is the closed set of view names, first being the one drawn unasked, or
    /// empty for a route with one picture and nothing to choose. It travels as route metadata so
    /// <see cref="PngQuery"/> can publish the query words beside the media type: the flag that makes a route
    /// answer a picture is the flag that documents how to ask for one, which is what keeps the two from
    /// drifting apart. The same array reaches <see cref="PngAnswer.AnsweredAsync"/>, so the enum the schema
    /// names and the names a refusal lists are one list.</para>
    ///
    /// <para>It attaches metadata and nothing else, deliberately. A second <c>Produces</c> for 200
    /// <b>replaces</b> the first rather than adding to it — as <see cref="WorldZipOrMapXml"/> below says —
    /// so declaring the picture that way took the JSON answer off a route that answers JSON by default, and
    /// the document read as though a preview could only ever hand back an image. <see cref="PngQuery"/> adds
    /// the media type to the 200 the endpoint's own response type already put there.</para></summary>
    public static RouteHandlerBuilder AlsoPng(this RouteHandlerBuilder builder, params string[] views) =>
        builder.WithMetadata(new PngPreview(views));

    /// <summary>The route answers its reading as characters on <c>?format=text</c>, beside its default — a
    /// picture or a JSON document. <see cref="TextQuery"/> publishes the word and the media type together,
    /// so the flag that makes a route answer text is the flag that documents how to ask for it.</summary>
    public static RouteHandlerBuilder AlsoText(this RouteHandlerBuilder builder) =>
        builder.WithMetadata(new TextTwin());

    /// <summary>The query words this route reads off the request rather than binding to a record — a
    /// magnification, a view name, the extent of a cut. <see cref="QueryWords"/> publishes them, so a knob
    /// that exists is a knob the schema names; declared here, beside the route, because the reader and the
    /// declaration drifting apart is the whole fault this exists to stop.</summary>
    public static RouteHandlerBuilder Reads(this RouteHandlerBuilder builder, params QueryWord[] words) =>
        builder.WithMetadata(new DeclaredQuery(words));

    /// <summary>The route answers a picture and nothing else.</summary>
    public static RouteHandlerBuilder Png(this RouteHandlerBuilder builder) =>
        builder.Produces(200, typeof(byte[]), "image/png");

    /// <summary>The route answers characters — a rendered board or a described flow, which is the one read a
    /// caller with no image reader can act on.</summary>
    public static RouteHandlerBuilder PlainText(this RouteHandlerBuilder builder) =>
        builder.Produces(200, typeof(string), "text/plain");

    /// <summary>The route answers the map document itself, as the XML a server loads.</summary>
    public static RouteHandlerBuilder MapXml(this RouteHandlerBuilder builder) =>
        builder.Produces(200, typeof(string), "application/xml");

    /// <summary>The route answers a whole world — <c>map.xml</c>, <c>level.dat</c> and the region files — for
    /// a map the studio synthesised, and the bare XML for a map that ships its own world. Both in one call
    /// because a status code carries one declaration: a second <c>Produces</c> for 200 replaces the first
    /// rather than adding to it.</summary>
    public static RouteHandlerBuilder WorldZipOrMapXml(this RouteHandlerBuilder builder) =>
        builder.Produces(200, typeof(byte[]), "application/zip", "application/xml");

    /// <summary>
    /// The refusals this route can answer, beyond the 400 and 500 every route publishes from one place.
    /// Each is a <see cref="RefusalDto"/> like those, so what a code adds is <b>which</b> refusal a caller
    /// must be ready for: <c>404</c> the subject named in the path is not stored, <c>409</c> an
    /// <c>If-Match</c> names a revision the document is no longer at, or a row is still bound elsewhere,
    /// <c>422</c> what is stored will not carry the request. The two import routes add their own.
    ///
    /// <para>Declared per route rather than derived from the path. A path holding <c>{slug}</c> nearly always
    /// answers 404, and nearly is a guess — a schema that guesses is the one thing a caller cannot act on,
    /// which is the whole reason the codes are written down at all. <see cref="Refusals"/> is where each is
    /// raised, and the <c>Fails with</c> column of every endpoint table in <c>docs/tools/</c> is held to what
    /// is declared here, so a row and a route cannot drift apart without failing a test.</para>
    /// </summary>
    public static RouteHandlerBuilder Refuses(this RouteHandlerBuilder builder, params int[] codes)
    {
        foreach (var code in codes) builder.Produces<RefusalDto>(code, "application/json");
        return builder;
    }
}
