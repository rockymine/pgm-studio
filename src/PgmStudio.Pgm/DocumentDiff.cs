using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm;

/// <summary>
/// The edits that take one version of a document to another, named the way a reader names the things in it.
///
/// <para>An element of a list is named by its <c>id</c> where every element carries a distinct one, so a shape
/// inserted ahead of the others is one <c>add</c> rather than every later shape changing; a list whose shared
/// ids changed order is one <c>set</c> of the whole list, because a shape's place is its draw order. A list
/// without ids is walked by index while its length holds and set whole where it does not. A list of
/// coordinates — an outline, a course, a ring — is one <c>set</c> saying how many of its points moved, how far,
/// and how many were inserted or removed. A placed thing whose <c>x</c> and <c>z</c> changed is one
/// <c>move</c> saying how far it went. Everything else is a member set, stated or removed by name.</para>
///
/// <para>A document that is absent reads as an empty one, so a plan stated for the first time is a set of each
/// of its members. Numbers compare by value, so a document re-serialized with <c>8.0</c> for <c>8</c> has not
/// changed.</para>
/// </summary>
public static class DocumentDiff
{
    /// <summary>The edits taking <paramref name="before"/> to <paramref name="after"/>, in the order the
    /// document states what they touch.</summary>
    public static IReadOnlyList<DocumentEdit> Between(string document, string? before, string? after)
    {
        var edits = new List<DocumentEdit>();
        new Walker(document, edits).Walk("", Parse(before), Parse(after));
        return edits;
    }

    /// <summary>The members whose list of coordinates is an outline or a course, and what their points are
    /// called in a sentence.</summary>
    private static readonly Dictionary<string, string> Outlines = new(StringComparer.Ordinal)
    {
        ["vertices"] = "vertices", ["points"] = "points", ["ring"] = "points",
    };

    /// <summary>Beyond this many cells an outline's points are counted rather than aligned.</summary>
    private const long AlignLimit = 1_000_000;

    private static JsonNode Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new JsonObject();
        try { return JsonNode.Parse(json) ?? new JsonObject(); }
        catch (JsonException) { return new JsonObject(); }
    }

    private sealed class Walker(string document, List<DocumentEdit> edits)
    {
        public void Walk(string path, JsonNode? before, JsonNode? after)
        {
            if (Same(before, after)) return;
            switch (before, after)
            {
                case (JsonObject was, JsonObject now):
                    Members(path, was, now);
                    return;
                case (JsonArray was, JsonArray now):
                    Elements(path, was, now);
                    return;
                default:
                    edits.Add(Edit(path, DocumentEdit.Set, after, before, Changed(Name(path), before, after)));
                    return;
            }
        }

        private void Members(string path, JsonObject was, JsonObject now)
        {
            var moved = Moved(path, was, now);
            foreach (var (key, value) in was)
            {
                if (moved && key is "x" or "z") continue;
                var member = Member(path, key);
                if (now.TryGetPropertyValue(key, out var next)) Walk(member, value, next);
                else edits.Add(Edit(member, DocumentEdit.Remove, null, value, $"{key} removed"));
            }
            foreach (var (key, value) in now)
                if (!was.ContainsKey(key))
                    edits.Add(Edit(Member(path, key), DocumentEdit.Set, value, null, Stated(key, value), stated: true));
        }

        /// <summary>A placed thing whose <c>x</c> and <c>z</c> changed, as one move — true where it was one.</summary>
        private bool Moved(string path, JsonObject was, JsonObject now)
        {
            if (Number(was["x"]) is not { } fromX || Number(was["z"]) is not { } fromZ
                || Number(now["x"]) is not { } toX || Number(now["z"]) is not { } toZ) return false;
            if (fromX == toX && fromZ == toZ) return false;

            var far = Math.Sqrt((toX - fromX) * (toX - fromX) + (toZ - fromZ) * (toZ - fromZ));
            edits.Add(new DocumentEdit(document, path, DocumentEdit.Move,
                JsonSerializer.SerializeToElement(new { x = now["x"], z = now["z"] }),
                $"moved {Blocks(far)}, from ({Text(fromX)}, {Text(fromZ)}) to ({Text(toX)}, {Text(toZ)})",
                JsonSerializer.SerializeToElement(new { x = was["x"], z = was["z"] })));
            return true;
        }

        private void Elements(string path, JsonArray was, JsonArray now)
        {
            if (Ids(was) is { } wasIds && Ids(now) is { } nowIds)
            {
                Keyed(path, was, now, wasIds, nowIds);
                return;
            }
            if (Outlines.TryGetValue(Name(path), out var noun) && Coordinates(was) && Coordinates(now))
            {
                edits.Add(Edit(path, DocumentEdit.Set, now, was, Outline(noun, Points(was), Points(now))));
                return;
            }
            if (was.Count == now.Count && was.All(node => node is JsonObject) && now.All(node => node is JsonObject))
            {
                for (var index = 0; index < was.Count; index++) Walk($"{path}[{index}]", was[index], now[index]);
                return;
            }
            edits.Add(Edit(path, DocumentEdit.Set, now, was, Listed(Name(path), was, now)));
        }

        private void Keyed(string path, JsonArray was, JsonArray now, List<string> wasIds, List<string> nowIds)
        {
            var wasSet = wasIds.ToHashSet(StringComparer.Ordinal);
            var nowSet = nowIds.ToHashSet(StringComparer.Ordinal);
            if (!wasIds.Where(nowSet.Contains).SequenceEqual(nowIds.Where(wasSet.Contains), StringComparer.Ordinal))
            {
                edits.Add(Edit(path, DocumentEdit.Set, now, was, $"{Name(path)} reordered"));
                return;
            }

            var what = Singular(Name(path));
            for (var index = 0; index < was.Count; index++)
                if (!nowSet.Contains(wasIds[index]))
                    edits.Add(Edit($"{path}[{wasIds[index]}]", DocumentEdit.Remove, null, was[index],
                        $"{Kind(was[index], what)} '{wasIds[index]}' removed"));

            var byId = wasIds.Select((id, index) => (id, index))
                .ToDictionary(pair => pair.id, pair => was[pair.index], StringComparer.Ordinal);
            for (var index = 0; index < now.Count; index++)
            {
                var id = nowIds[index];
                if (byId.TryGetValue(id, out var held)) Walk($"{path}[{id}]", held, now[index]);
                else edits.Add(Edit(path, DocumentEdit.Add, now[index], null, $"{Kind(now[index], what)} '{id}' added",
                    stated: true));
            }
        }

        private DocumentEdit Edit(string path, string op, JsonNode? value, JsonNode? before, string says,
                                  bool stated = false) =>
            new(document, path, op, JsonSerializer.SerializeToElement(value), says,
                stated ? null : JsonSerializer.SerializeToElement(before));
    }

    /// <summary>The ids of a list whose every element is an object carrying a distinct non-empty one, or null
    /// where any does not — which is a list walked by index instead.</summary>
    private static List<string>? Ids(JsonArray list)
    {
        var ids = new List<string>(list.Count);
        foreach (var element in list)
        {
            if (element is not JsonObject item || item["id"] is not JsonValue stated
                || !stated.TryGetValue<string>(out var id) || id.Length == 0) return null;
            ids.Add(id);
        }
        return ids.Distinct(StringComparer.Ordinal).Count() == ids.Count ? ids : null;
    }

    private static bool Same(JsonNode? before, JsonNode? after)
    {
        if (Number(before) is { } left && Number(after) is { } right) return left == right;
        return (before, after) switch
        {
            (JsonObject was, JsonObject now) => was.Count == now.Count
                && was.All(pair => now.TryGetPropertyValue(pair.Key, out var other) && Same(pair.Value, other)),
            (JsonArray was, JsonArray now) => was.Count == now.Count
                && was.Zip(now).All(pair => Same(pair.First, pair.Second)),
            _ => JsonNode.DeepEquals(before, after),
        };
    }

    private static double? Number(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.Number && value.TryGetValue<double>(out var number)
            ? number : null;

    private static bool Coordinates(JsonArray list) =>
        list.All(point => point is JsonArray pair && pair.Count is 2 or 3 && pair.All(axis => Number(axis) is not null));

    private static List<double[]> Points(JsonArray list) =>
        [.. list.Select(point => point!.AsArray().Select(axis => Number(axis)!.Value).ToArray())];

    /// <summary>How an outline changed. One that kept its number of points is paired by place, so a point is the
    /// same point wherever the others went, and one whose every point moved by one offset moved whole. One that
    /// gained or lost points is aligned where it stayed, and the rest paired off within each stretch that differs
    /// — a pair is a point that moved, the remainder points inserted or removed.</summary>
    private static string Outline(string noun, List<double[]> was, List<double[]> now)
    {
        if (was.Count == now.Count)
        {
            var shifted = Enumerable.Range(0, was.Count).Where(index => !was[index].SequenceEqual(now[index])).ToList();
            if (shifted.Count == 0) return $"{noun} changed";
            var offsets = shifted.Select(index => (X: now[index][0] - was[index][0], Z: now[index][1] - was[index][1]))
                .Distinct().ToList();
            if (shifted.Count == was.Count && offsets.Count == 1)
                return $"all {was.Count} {noun} moved {Blocks(Math.Sqrt(offsets[0].X * offsets[0].X + offsets[0].Z * offsets[0].Z))}, "
                       + $"by ({Text(offsets[0].X)}, {Text(offsets[0].Z)})";
            return $"{shifted.Count} of {was.Count} {noun} moved "
                   + $"(up to {Blocks(shifted.Max(index => Distance(was[index], now[index])))})";
        }

        var lead = 0;
        while (lead < was.Count && lead < now.Count && was[lead].SequenceEqual(now[lead])) lead++;
        var trail = 0;
        while (trail < was.Count - lead && trail < now.Count - lead
               && was[was.Count - 1 - trail].SequenceEqual(now[now.Count - 1 - trail])) trail++;
        var middleWas = was.GetRange(lead, was.Count - lead - trail);
        var middleNow = now.GetRange(lead, now.Count - lead - trail);
        if ((long)middleWas.Count * middleNow.Count > AlignLimit)
            return $"{was.Count} → {now.Count} {noun}";

        int moved = 0, inserted = 0, removed = 0;
        var farthest = 0.0;
        foreach (var (gone, come) in Stretches(middleWas, middleNow))
        {
            var paired = Math.Min(gone.Count, come.Count);
            for (var index = 0; index < paired; index++)
                farthest = Math.Max(farthest, Distance(gone[index], come[index]));
            moved += paired;
            removed += gone.Count - paired;
            inserted += come.Count - paired;
        }

        var parts = new List<string>();
        if (moved > 0) parts.Add($"{moved} of {was.Count} {noun} moved (up to {Blocks(farthest)})");
        if (inserted > 0) parts.Add($"{inserted} inserted");
        if (removed > 0) parts.Add($"{removed} removed");
        return parts.Count > 0 ? string.Join(", ", parts) : $"{noun} changed";
    }

    /// <summary>The stretches two point lists differ over, each as the points only the first holds there and
    /// the points only the second does, from their longest common run.</summary>
    private static IEnumerable<(List<double[]> Gone, List<double[]> Come)> Stretches(
        List<double[]> was, List<double[]> now)
    {
        var common = new int[was.Count + 1, now.Count + 1];
        for (var i = was.Count - 1; i >= 0; i--)
            for (var j = now.Count - 1; j >= 0; j--)
                common[i, j] = was[i].SequenceEqual(now[j])
                    ? common[i + 1, j + 1] + 1
                    : Math.Max(common[i + 1, j], common[i, j + 1]);

        List<double[]> gone = [], come = [];
        int at = 0, to = 0;
        while (at < was.Count || to < now.Count)
        {
            if (at < was.Count && to < now.Count && was[at].SequenceEqual(now[to]))
            {
                if (gone.Count + come.Count > 0) { yield return (gone, come); gone = []; come = []; }
                at++;
                to++;
            }
            else if (to < now.Count && (at == was.Count || common[at, to + 1] >= common[at + 1, to]))
                come.Add(now[to++]);
            else
                gone.Add(was[at++]);
        }
        if (gone.Count + come.Count > 0) yield return (gone, come);
    }

    private static double Distance(double[] from, double[] to) =>
        Math.Sqrt(from.Zip(to).Sum(pair => (pair.Second - pair.First) * (pair.Second - pair.First)));

    private static string Changed(string name, JsonNode? before, JsonNode? after) =>
        Scalar(before) && Scalar(after) ? $"{name} {Show(before)} → {Show(after)}" : $"{name} changed";

    private static string Stated(string name, JsonNode? value) =>
        Scalar(value) ? $"{name} stated as {Show(value)}" : $"{name} stated";

    private static string Listed(string name, JsonArray was, JsonArray now)
    {
        if (was.All(Scalar) && now.All(Scalar) && was.Count <= 8 && now.Count <= 8)
            return $"{name} [{string.Join(", ", was.Select(Show))}] → [{string.Join(", ", now.Select(Show))}]";
        return $"{name}: {was.Count} → {now.Count} entries";
    }

    private static bool Scalar(JsonNode? node) => node is null or JsonValue;

    private static string Show(JsonNode? node)
    {
        if (node is null) return "null";
        if (Number(node) is { } number) return Text(number);
        var text = node is JsonValue value && value.TryGetValue<string>(out var word) ? word : node.ToJsonString();
        return text.Length > 40 ? text[..39] + "…" : text;
    }

    private static string Text(double number) => number.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Blocks(double far) => $"{Text(Math.Round(far, 1))} block{(Math.Round(far, 1) == 1 ? "" : "s")}";

    private static string Member(string path, string key) => path.Length == 0 ? key : $"{path}.{key}";

    /// <summary>The last member a path names, without the element it addresses.</summary>
    private static string Name(string path)
    {
        var last = path[(path.LastIndexOf('.') + 1)..];
        return last.Contains('[') ? last[..last.IndexOf('[')] : last;
    }

    /// <summary>What a list's element is called: its own <c>type</c> or <c>kind</c> where it states one, and
    /// otherwise the list's name made singular.</summary>
    private static string Kind(JsonNode? element, string fallback)
    {
        foreach (var field in (string[])["type", "kind"])
            if (element?[field] is JsonValue stated && stated.TryGetValue<string>(out var word) && word.Length > 0)
                return word;
        return fallback;
    }

    private static string Singular(string name) =>
        name.EndsWith("xes", StringComparison.Ordinal) ? name[..^2]
        : name.EndsWith('s') ? name[..^1]
        : name.Length > 0 ? name : "entry";
}
