using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

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
    /// <see cref="PngAnswer"/> for the parameter and its refusal.</summary>
    public static RouteHandlerBuilder AlsoPng(this RouteHandlerBuilder builder) =>
        builder.Produces(200, typeof(byte[]), "image/png");

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
}
