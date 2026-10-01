using System.Text.Json;
using System.Text.Json.Serialization;

namespace PgmStudio.Vocabulary;

/// <summary>The four documents a studio-authored map is stated in: the plan it was drawn from, the refinement
/// stating what the plan cannot, the layout that is built, and the intent it is played for. Spelled by the
/// studio that keeps them, the edits that name one, and every reader of a map's changes.</summary>
public static class MapDocuments
{
    public const string Plan = "plan", Refinement = "refinement", Layout = "layout", Intent = "intent";

    /// <summary>The four, in the order a map is stated in them.</summary>
    public static readonly string[] All = [Plan, Refinement, Layout, Intent];
}

/// <summary>
/// One mechanical change to one of the four documents at one path: a change a gate states as a finding's fix,
/// or one that landed between two of a map's changes.
///
/// <para><b>Document</b> names which of the four the path is into (<see cref="MapDocuments"/>). <b>Path</b> is
/// the field the change lands on, spelled the way an unread field is (<c>relief.team.marks</c>,
/// <c>dressing.props[erratic-broken]</c>): members joined by dots, an array element by its <c>id</c> in brackets
/// where the element carries one and by its index otherwise. <b>Op</b> is one of four: <c>add</c> appends
/// <see cref="Value"/> to the array at the path; <c>set</c> replaces the value at the path with it; <c>move</c>
/// sets the <c>x</c> and <c>z</c> the value carries on the object at the path; <c>remove</c> takes away what the
/// path names, and its value is <c>null</c>. <b>Before</b> is what the path held, carried by an edit that
/// landed and absent from one a gate proposes. <b>Says</b> is the change in the author's terms, one
/// sentence.</para>
/// </summary>
/// <param name="Document">Which document: <c>plan</c>, <c>refinement</c>, <c>layout</c> or <c>intent</c>.</param>
/// <param name="Path">Where in it the change lands.</param>
/// <param name="Op"><c>add</c>, <c>set</c>, <c>move</c> or <c>remove</c>.</param>
/// <param name="Value">What is added, set or moved to, as the document would carry it.</param>
/// <param name="Says">The change in words.</param>
/// <param name="Before">What the path held before the change, where it held anything.</param>
public sealed record DocumentEdit(
    string Document, string Path, string Op, JsonElement Value, string Says,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] JsonElement? Before = null)
{
    public const string Add = "add", Set = "set", Move = "move", Remove = "remove";

    /// <summary>An edit whose value is built from an anonymous object or a dictionary, serialized the way
    /// the document states it.</summary>
    public static DocumentEdit Of(string document, string path, string op, object value, string says) =>
        new(document, path, op, JsonSerializer.SerializeToElement(value), says);
}
