using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PgmStudio.Api.Services;

/// <summary>
/// The Python kit the studio serves at <c>GET /api/kit.py</c>, written from its own published document.
///
/// <para>One constructor per shape a route takes, by the studio's own field names, writing only what the caller
/// stated so a field left out stays the studio's default; each checks a word against its set and a value against
/// its type before anything is sent, and a polymorphic shape is built through its leaves. <c>Studio</c> calls
/// every operation under a name taken from its id, waits out a <c>429</c>, prints a success's <c>warnings</c> and
/// raises a refusal with its findings. <c>find</c> searches every description the document carries, and
/// <c>build</c> rebuilds a document through the constructors.</para>
///
/// <para>It is written from the document rather than from the types, so what the kit names is what the schema
/// names, and it is labelled with the document's hash, so a kit and the studio that served it can be
/// matched.</para>
/// </summary>
public static class PythonKit
{
    /// <summary>The kit for a document, and the hash of the document it was written from.</summary>
    public static (string Script, string Hash) Write(string documentJson)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(documentJson)))[..16];
        var document = JsonNode.Parse(documentJson)!.AsObject();
        var schemas = document["components"]?["schemas"]?.AsObject() ?? [];
        var posted = Posted(document, schemas);

        var script = new StringBuilder();
        script.Append(Preamble.Replace("{hash}", hash, StringComparison.Ordinal));
        script.Append("_SHAPES = json.loads(").Append(Literal(Shapes(schemas, posted).ToJsonString())).Append(")\n");
        script.Append("_INDEX = json.loads(").Append(Literal(Index(document, schemas).ToJsonString())).Append(")\n\n");
        script.Append(Runtime);
        foreach (var name in posted.Order(StringComparer.Ordinal))
            if (Constructor(name, schemas) is { } constructor) script.Append(constructor);
        script.Append(Client);
        foreach (var (path, verb, operation) in Operations(document))
            script.Append(Method(path, verb, operation));
        return (script.ToString(), hash);
    }

    // ── what the kit is made of ──────────────────────────────────────────────────────────────────────────

    private static IEnumerable<(string Path, string Verb, JsonObject Operation)> Operations(JsonObject document)
    {
        foreach (var (path, item) in document["paths"]?.AsObject() ?? [])
            foreach (var (verb, operation) in item?.AsObject() ?? [])
                if (operation is JsonObject found && found.ContainsKey("responses")) yield return (path, verb, found);
    }

    /// <summary>Every schema a route reads a body as, following the references and a base's leaves down.</summary>
    private static SortedSet<string> Posted(JsonObject document, JsonObject schemas)
    {
        var found = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var (_, verb, operation) in Operations(document))
            if (verb is "post" or "put" or "patch" && operation["requestBody"] is { } body) References(body, found);
        for (var grew = true; grew;)
        {
            grew = false;
            foreach (var name in found.ToList())
            {
                var nested = new HashSet<string>(StringComparer.Ordinal);
                References(schemas[name], nested);
                foreach (var leaf in Leaves(schemas[name])) nested.Add(leaf.Value);
                foreach (var reference in nested) grew |= found.Add(reference);
            }
        }
        return found;
    }

    private static void References(JsonNode? node, ISet<string> into)
    {
        switch (node)
        {
            case JsonObject members:
                foreach (var (key, value) in members)
                    if (key == "$ref" && value is JsonValue reference) into.Add(Named(reference));
                    else References(value, into);
                break;
            case JsonArray items:
                foreach (var item in items) References(item, into);
                break;
        }
    }

    private static string Named(JsonNode reference) => reference.GetValue<string>().Split('/')[^1];

    /// <summary>The leaves a polymorphic base's discriminator maps its words to.</summary>
    private static Dictionary<string, string> Leaves(JsonNode? schema) =>
        schema?["discriminator"]?["mapping"] is JsonObject mapping
            ? mapping.ToDictionary(pair => pair.Key, pair => Named(pair.Value!), StringComparer.Ordinal)
            : [];

    /// <summary>The base a leaf is the leaf of, its discriminator's name, and the word the leaf answers to.</summary>
    private static (string Base, string By, string Word)? Leaf(string name, JsonObject schemas)
    {
        foreach (var (candidate, schema) in schemas)
            foreach (var (word, leaf) in Leaves(schema))
                if (leaf == name)
                    return (candidate, schema!["discriminator"]!["propertyName"]!.GetValue<string>(), word);
        return null;
    }

    /// <summary>A record's fields, its base's included: its own, and each part of its <c>allOf</c> — the ones a
    /// caller states, or with <paramref name="computed"/> the ones the studio computes as well.</summary>
    private static List<KeyValuePair<string, JsonObject>> Fields(
        string name, JsonObject schemas, bool computed = false, HashSet<string>? seen = null)
    {
        seen ??= [];
        var fields = new List<KeyValuePair<string, JsonObject>>();
        if (!seen.Add(name) || schemas[name] is not JsonObject schema) return fields;
        Collect(schema, schemas, computed, seen, fields);
        return fields;
    }

    private static void Collect(JsonObject schema, JsonObject schemas, bool computed, HashSet<string> seen,
        List<KeyValuePair<string, JsonObject>> into)
    {
        if (schema["properties"] is JsonObject own)
            foreach (var (field, value) in own)
                if (value is JsonObject declared && (computed || !ReadOnly(declared)) && into.All(pair => pair.Key != field))
                    into.Add(new(field, declared));
        foreach (var part in (schema["allOf"] as JsonArray ?? []).OfType<JsonObject>())
        {
            if (part["$ref"] is not { } reference)
            {
                Collect(part, schemas, computed, seen, into);
                continue;
            }
            foreach (var inherited in Fields(Named(reference), schemas, computed, seen))
                if (into.All(pair => pair.Key != inherited.Key)) into.Add(inherited);
        }
    }

    /// <summary>A field the studio writes and reads nothing from, which a caller never states.</summary>
    private static bool ReadOnly(JsonObject field) =>
        field["readOnly"] is JsonValue flag && flag.TryGetValue<bool>(out var readOnly) && readOnly;

    // ── the shapes, as data the runtime checks against ───────────────────────────────────────────────────

    /// <summary>Per shape: a record's fields and what each takes, a leaf's word, a base's leaves, and for a
    /// shape that is a value rather than a record — a closed set, a rectangle in a row — what it takes.</summary>
    private static JsonObject Shapes(JsonObject schemas, IEnumerable<string> posted)
    {
        var shapes = new JsonObject();
        foreach (var name in posted)
        {
            if (schemas[name] is not JsonObject schema) continue;
            var shape = new JsonObject();
            var leaves = Leaves(schema);
            if (leaves.Count > 0)
            {
                shape["by"] = schema["discriminator"]!["propertyName"]!.DeepClone();
                shape["leaves"] = new JsonObject(leaves.Select(pair =>
                    KeyValuePair.Create(pair.Key, (JsonNode?)JsonValue.Create(pair.Value))));
            }
            else if (IsRecord(schema))
            {
                var fields = new JsonObject();
                var renamed = new JsonObject();
                foreach (var (field, declared) in Fields(name, schemas, computed: true))
                {
                    fields[field] = Takes(declared);
                    if (ReadOnly(declared)) fields[field]!["readOnly"] = true;
                    if (Parameter(field) != field) renamed[field] = Parameter(field);
                }
                shape["fields"] = fields;
                if (renamed.Count > 0) shape["params"] = renamed;
                if (Leaf(name, schemas) is { } leaf)
                {
                    shape["by"] = leaf.By;
                    shape["kind"] = leaf.Word;
                }
            }
            else shape["takes"] = Takes(schema);
            shapes[name] = shape;
        }
        return shapes;
    }

    private static bool IsRecord(JsonObject schema) =>
        schema["properties"] is JsonObject || schema["allOf"] is JsonArray
        || schema["type"]?.GetValue<string>() == "object" && schema["additionalProperties"] is not JsonObject;

    /// <summary>What a field takes: a type, a reference, words, items, values, or a combination of them.</summary>
    private static JsonObject Takes(JsonObject schema)
    {
        var takes = new JsonObject();
        if (schema["$ref"] is { } reference) takes["ref"] = Named(reference);
        if (schema["type"] is JsonValue type) takes["type"] = type.DeepClone();
        if (schema["enum"] is JsonArray words) takes["words"] = words.DeepClone();
        if (schema["nullable"] is JsonValue nullable && nullable.GetValue<bool>()) takes["null"] = true;
        if (schema["items"] is JsonObject items) takes["items"] = Takes(items);
        if (schema["additionalProperties"] is JsonObject values) takes["values"] = Takes(values);
        foreach (var combined in (string[])["oneOf", "anyOf", "allOf"])
            if (schema[combined] is JsonArray parts)
                takes[combined] = new JsonArray([.. parts.OfType<JsonObject>().Select(part => (JsonNode)Takes(part))]);
        return takes;
    }

    // ── find ─────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Every description the document carries, as <c>[what, name, text]</c>.</summary>
    private static JsonArray Index(JsonObject document, JsonObject schemas)
    {
        var index = new JsonArray();
        foreach (var (name, schema) in schemas)
        {
            if (schema is not JsonObject declared) continue;
            index.Add(new JsonArray("shape", name, OneLine(Description(declared))));
            var parts = (declared["allOf"] as JsonArray ?? []).OfType<JsonObject>().Prepend(declared);
            foreach (var part in parts)
                foreach (var (field, value) in part["properties"] as JsonObject ?? [])
                    if (value is JsonObject described)
                        index.Add(new JsonArray("field", $"{name}.{field}", OneLine(Description(described))));
        }
        foreach (var (path, verb, operation) in Operations(document))
        {
            index.Add(new JsonArray("route", $"{verb.ToUpperInvariant()} {path} = Studio.{MethodName(operation, path, verb)}",
                OneLine($"{Text(operation["summary"])} {Text(operation["description"])}")));
            foreach (var parameter in (operation["parameters"] as JsonArray ?? []).OfType<JsonObject>())
                if (parameter["in"]?.GetValue<string>() == "query")
                    index.Add(new JsonArray("word", $"{verb.ToUpperInvariant()} {path} ?{Text(parameter["name"])}",
                        OneLine(Text(parameter["description"]))));
        }
        return index;
    }

    private static string Description(JsonObject schema) =>
        Text(schema["description"]) is { Length: > 0 } own ? own
        : (schema["allOf"] as JsonArray ?? []).OfType<JsonObject>().Select(part => Text(part["description"]))
            .FirstOrDefault(text => text.Length > 0) ?? "";

    private static string Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";

    // ── constructors ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>A record's constructor; a base is built through its leaves and a value needs none.</summary>
    private static string? Constructor(string name, JsonObject schemas)
    {
        if (schemas[name] is not JsonObject schema || Leaves(schema).Count > 0 || !IsRecord(schema)) return null;
        var leaf = Leaf(name, schemas);
        var fields = Fields(name, schemas).Where(field => field.Key != leaf?.By).ToList();

        var text = new StringBuilder();
        text.Append("def ").Append(name).Append('(');
        if (fields.Count > 0) text.Append('*');
        foreach (var (field, _) in fields) text.Append(", ").Append(Parameter(field)).Append("=_MISSING");
        text.Append("):\n    ").Append(Docstring(Description(schema), fields, leaf)).Append('\n');
        text.Append("    return _stated(").Append(Literal(name)).Append(", {");
        var entries = fields.Select(field => $"{Literal(field.Key)}: {Parameter(field.Key)}").ToList();
        if (leaf is { } known) entries.Insert(0, $"{Literal(known.By)}: {Literal(known.Word)}");
        text.Append(string.Join(", ", entries)).Append("})\n\n\n");
        return text.ToString();
    }

    private static string Docstring(
        string description, List<KeyValuePair<string, JsonObject>> fields, (string Base, string By, string Word)? leaf)
    {
        var text = new StringBuilder(Trimmed(description));
        if (leaf is { } known) text.Append($"\n\nA {known.Base} whose {known.By} is {known.Word}.");
        if (fields.Count > 0) text.Append('\n');
        foreach (var (field, schema) in fields)
        {
            text.Append($"\n{Parameter(field)}: ").Append(OneLine(Description(schema)));
            var words = ((schema["enum"] ?? schema["items"]?["enum"]) as JsonArray ?? [])
                .Select(word => word!.ToJsonString()).ToList();
            if (words.Count > 0) text.Append($" One of {string.Join(", ", words)}.");
        }
        return Docstring(text.ToString());
    }

    /// <summary>A description on one line: the documentation's own line breaks and indents are layout.</summary>
    private static string OneLine(string text) => string.Join(' ', text.Split((char[])[' ', '\n', '\r', '\t'],
        StringSplitOptions.RemoveEmptyEntries));

    /// <summary>A description with each line trimmed and blank lines kept as paragraph breaks.</summary>
    private static string Trimmed(string text) =>
        string.Join('\n', text.Split('\n').Select(line => line.Trim())).Trim();

    private static string Docstring(string text) =>
        "\"\"\"" + text.Replace("\\", "\\\\").Replace("\"\"\"", "\\\"\\\"\\\"").TrimEnd('"', '\\') + "\n    \"\"\"";

    // ── the client ───────────────────────────────────────────────────────────────────────────────────────

    private static string Method(string path, string verb, JsonObject operation)
    {
        var name = MethodName(operation, path, verb);
        var parameters = (operation["parameters"] as JsonArray ?? []).OfType<JsonObject>().ToList();
        var used = new HashSet<string>(StringComparer.Ordinal) { "self" };
        string Unique(string wire)
        {
            var identifier = Parameter(wire);
            while (!used.Add(identifier)) identifier += "_";
            return identifier;
        }

        var inPath = parameters.Where(parameter => Text(parameter["in"]) == "path")
            .Select(parameter => (Wire: Text(parameter["name"]), Name: Unique(Text(parameter["name"])))).ToList();
        var body = operation["requestBody"] is null ? null : Unique("body");
        var inQuery = parameters.Where(parameter => Text(parameter["in"]) == "query")
            .Select(parameter => (Wire: Text(parameter["name"]), Name: Unique(Text(parameter["name"])))).ToList();

        var text = new StringBuilder();
        text.Append("    def ").Append(name).Append("(self");
        foreach (var (_, identifier) in inPath) text.Append(", ").Append(identifier);
        if (body is not null) text.Append(", ").Append(body).Append("=None");
        if (inQuery.Count > 0) text.Append(", *");
        foreach (var (_, identifier) in inQuery) text.Append(", ").Append(identifier).Append("=None");
        text.Append("):\n        ");
        var summary = Trimmed(Text(operation["summary"]));
        text.Append(Docstring(summary.Length > 0 ? summary : $"{verb.ToUpperInvariant()} {path}")
            .Replace("\n    \"\"\"", "\n        \"\"\""));
        text.Append("\n        return self._call(").Append(Literal(verb.ToUpperInvariant())).Append(", ").Append(Literal(path));
        text.Append(", {").Append(string.Join(", ", inPath.Select(pair => $"{Literal(pair.Wire)}: {pair.Name}"))).Append('}');
        text.Append(", {").Append(string.Join(", ", inQuery.Select(pair => $"{Literal(pair.Wire)}: {pair.Name}"))).Append('}');
        text.Append(", ").Append(body ?? "None").Append(")\n\n");
        return text.ToString();
    }

    /// <summary>A method name from an operation id: <c>putMapSource</c> is <c>put_map_source</c>.</summary>
    private static string MethodName(JsonObject operation, string path, string verb)
    {
        var id = Text(operation["operationId"]) is { Length: > 0 } stated ? stated : verb + path;
        var text = new StringBuilder();
        foreach (var character in id)
            if (char.IsUpper(character)) text.Append('_').Append(char.ToLowerInvariant(character));
            else text.Append(char.IsLetterOrDigit(character) ? character : '_');
        return text.ToString().Trim('_');
    }

    private static readonly HashSet<string> Reserved =
    [
        "False", "None", "True", "and", "as", "assert", "async", "await", "break", "class", "continue", "def", "del",
        "elif", "else", "except", "finally", "for", "from", "global", "if", "import", "in", "is", "lambda",
        "nonlocal", "not", "or", "pass", "raise", "return", "try", "while", "with", "yield", "self",
    ];

    /// <summary>A field or a word as a Python parameter: as the studio spells it, with a trailing underscore
    /// where that spelling is a keyword.</summary>
    private static string Parameter(string name)
    {
        var identifier = new string([.. name.Select(character => char.IsAsciiLetterOrDigit(character) ? character : '_')]);
        if (identifier.Length == 0 || char.IsAsciiDigit(identifier[0])) identifier = "_" + identifier;
        return Reserved.Contains(identifier) ? identifier + "_" : identifier;
    }

    /// <summary>A Python string literal, which a JSON string is.</summary>
    private static string Literal(string text) => JsonSerializer.Serialize(text);

    // ── the fixed text ───────────────────────────────────────────────────────────────────────────────────

    private const string Preamble = """""
        """pgm-studio kit, written by the studio from its own schema (/api/openapi/v1.json, {hash}).

        Constructors build the documents the studio reads, by the studio's own field names, and write only what is
        stated, so a field left out is the studio's default rather than a guess. Each checks a word against its set
        and a value against its type before anything is sent, and a polymorphic shape is built through its leaves:
        SolidMaterial(id=12) is {"kind": "solid", "id": 12}. library() and use() are a refinement's names.

            studio = Studio()                      # PGM_STUDIO_API, and PGM_STUDIO_TOKEN where one is needed
            studio.put_map_source("weirgate", {"plan": plan, "refinement": Refinement(created="2026-10-01")})

        Studio calls every route under a name taken from it, waits out a 429, prints a success's warnings and
        raises Refusal with the findings of a refusal. find("coast") lists what the studio says about a word, and
        build(shape, document) rebuilds a document through the constructors.
        """
        from __future__ import annotations

        import json
        import os
        import sys
        import time
        import urllib.error
        import urllib.parse
        import urllib.request

        SCHEMA = "{hash}"


        class _Missing:
            def __repr__(self):
                return "<not stated>"


        _MISSING = _Missing()

        """"" + "\n";

    private const string Runtime = """""
        _TYPES = {"string": (str,), "integer": (int,), "number": (int, float), "boolean": (bool,),
                  "array": (list, tuple), "object": (dict,)}


        def _fits(value, takes):
            """Whether a value is one a field takes, and if not, why."""
            if value is None:
                return True, ""
            if "ref" in takes:
                shape = _SHAPES.get(takes["ref"], {})
                if "takes" in shape:
                    return _fits(value, shape["takes"])
                if isinstance(value, dict) or not shape:
                    return True, ""
                return False, f"is a {type(value).__name__}, not a {takes['ref']}"
            for combined in ("oneOf", "anyOf"):
                if combined in takes:
                    reasons = [why for ok, why in (_fits(value, part) for part in takes[combined]) if not ok]
                    if len(reasons) == len(takes[combined]):
                        return False, " and ".join(dict.fromkeys(reasons))
                    return True, ""
            if "allOf" in takes:
                for part in takes["allOf"]:
                    ok, why = _fits(value, part)
                    if not ok:
                        return False, why
            if "words" in takes and value not in takes["words"]:
                return False, f"is {value!r}, not one of {', '.join(map(repr, takes['words']))}"
            kind = takes.get("type")
            if kind in _TYPES:
                if isinstance(value, bool) and kind in ("integer", "number"):
                    return False, f"is a flag, not a {kind}"
                if not isinstance(value, _TYPES[kind]):
                    return False, f"is a {type(value).__name__}, not a {kind}"
            if "items" in takes and isinstance(value, (list, tuple)):
                for index, item in enumerate(value):
                    ok, why = _fits(item, takes["items"])
                    if not ok:
                        return False, f"holds at [{index}] a value that {why}"
            if "values" in takes and isinstance(value, dict):
                for key, item in value.items():
                    ok, why = _fits(item, takes["values"])
                    if not ok:
                        return False, f"holds at {key!r} a value that {why}"
            return True, ""


        def _stated(shape, values):
            """The fields stated, each checked against what the shape takes; a field left out is not written."""
            fields = _SHAPES[shape]["fields"]
            stated = {}
            for name, value in values.items():
                if value is _MISSING:
                    continue
                if name in fields:
                    ok, why = _fits(value, fields[name])
                    if not ok:
                        raise ValueError(f"{shape}.{name} {why}")
                stated[name] = value
            return stated


        def library(name, **beside):
            """A row of the studio's library, by its name or its id, standing where a refinement states a material, a
            theme, a room style, a prop style or a biome; the fields stated beside it are laid over the copy."""
            return {"library": name, **beside}


        def use(name, **beside):
            """A material the refinement's own `materials` states, by its name there, with fields laid over it."""
            return {"use": name, **beside}


        def find(text, show=True):
            """Every shape, field, route and query word whose name or description mentions `text`."""
            needle = text.lower()
            found = [entry for entry in _INDEX if needle in entry[1].lower() or needle in entry[2].lower()]
            if show:
                for what, name, description in found:
                    print(f"{what:5} {name}\n      {description[:240]}")
            return found


        def build(shape, value):
            """`value` rebuilt through the constructors as the shape it is, every field checked on the way. A field the
            studio computes is no constructor's to take, and is carried through as it was."""
            if not isinstance(value, dict) or "library" in value or "use" in value:
                return value
            spec = _SHAPES.get(shape)
            if spec is None or "takes" in spec:
                return value
            if "leaves" in spec:
                word = value.get(spec["by"])
                if word not in spec["leaves"]:
                    raise ValueError(f"{shape} takes {spec['by']} as one of "
                                     f"{', '.join(map(repr, spec['leaves']))}, not {word!r}")
                return build(spec["leaves"][word], value)
            renamed = spec.get("params", {})
            arguments, computed = {}, {}
            for name, item in value.items():
                if name == spec.get("by"):
                    continue
                if name not in spec["fields"]:
                    raise ValueError(f"{shape} has no field {name!r}")
                if spec["fields"][name].get("readOnly"):
                    computed[name] = item
                    continue
                arguments[renamed.get(name, name)] = _built(item, spec["fields"][name])
            built = globals()[shape](**arguments)
            built.update(computed)
            return built


        def _built(value, takes):
            if value is None:
                return None
            if "ref" in takes:
                return build(takes["ref"], value)
            if "allOf" in takes and isinstance(value, dict):
                built, rest = {}, dict(value)
                for part in takes["allOf"]:
                    fields = _SHAPES.get(part.get("ref"), {}).get("fields", {})
                    mine = {key: rest.pop(key) for key in list(rest) if key in fields}
                    built.update(build(part["ref"], mine) if "ref" in part else mine)
                if rest:
                    raise ValueError(f"no shape here takes {', '.join(map(repr, rest))}")
                return built
            for combined in ("oneOf", "anyOf"):
                if combined in takes:
                    reasons = []
                    for part in takes[combined]:
                        ok, why = _fits(value, part)
                        if not ok:
                            reasons.append(why)
                            continue
                        try:
                            return _built(value, part)
                        except (ValueError, TypeError) as refused:
                            reasons.append(str(refused))
                    raise ValueError(" and ".join(reasons))
            if "items" in takes and isinstance(value, list):
                return [_built(item, takes["items"]) for item in value]
            if "values" in takes and isinstance(value, dict):
                return {key: _built(item, takes["values"]) for key, item in value.items()}
            return value


        """"";

    private const string Client = """""
        class Refusal(Exception):
            """A request the studio refused: its status, its error and message, and its findings — each a rule id,
            which GET /api/rules explains, and what it found."""

            def __init__(self, status, answer):
                self.status = status
                self.answer = answer if isinstance(answer, dict) else {"message": str(answer)}
                self.error = self.answer.get("error")
                self.findings = self.answer.get("findings") or []
                lines = [f"{status} {self.error or ''}: {self.answer.get('message', '')}"]
                lines += [f"  {finding.get('rule', '')} {finding.get('message', '')}" for finding in self.findings]
                super().__init__("\n".join(lines))


        def _word(value):
            if isinstance(value, bool):
                return "true" if value else "false"
            if isinstance(value, (list, tuple)):
                return ",".join(map(str, value))
            return str(value)


        class Studio:
            """Every route of the studio, each a method named after it: PUT /map/{slug}/source is put_map_source.
            A path's parameters are its arguments, a body is `body`, and a query word is a keyword."""

            def __init__(self, base=None, token=None, quiet=False):
                self.base = (base or os.environ.get("PGM_STUDIO_API") or "").rstrip("/")
                if not self.base:
                    raise ValueError("no studio named: pass base, or set PGM_STUDIO_API to the studio's /api root")
                self.token = token or os.environ.get("PGM_STUDIO_TOKEN")
                self.quiet = quiet

            def _call(self, verb, path, route, query, body):
                for key, value in route.items():
                    path = path.replace("{" + key + "}", urllib.parse.quote(str(value), safe=""))
                url = self.base + (path[len("/api"):] if path.startswith("/api/") else path)
                words = {key: _word(value) for key, value in query.items() if value is not None}
                if words:
                    url += "?" + urllib.parse.urlencode(words)
                data = None if body is None else json.dumps(body).encode()
                while True:
                    request = urllib.request.Request(url, data=data, method=verb)
                    if data is not None:
                        request.add_header("Content-Type", "application/json")
                    if self.token:
                        request.add_header("Authorization", f"Bearer {self.token}")
                    try:
                        with urllib.request.urlopen(request) as response:
                            return self._answer(response.headers, response.read())
                    except urllib.error.HTTPError as refused:
                        if refused.code == 429:
                            time.sleep(float(refused.headers.get("Retry-After") or 1))
                            continue
                        raw = refused.read()
                        try:
                            answer = json.loads(raw)
                        except ValueError:
                            answer = raw.decode(errors="replace")
                        raise Refusal(refused.code, answer) from None

            def _answer(self, headers, raw):
                media = (headers.get("Content-Type") or "").split(";")[0]
                if "json" in media:
                    answer = json.loads(raw) if raw else None
                    if isinstance(answer, dict) and answer.get("warnings") and not self.quiet:
                        for finding in answer["warnings"]:
                            print(f"warning {finding.get('rule', '')}: {finding.get('message', '')}", file=sys.stderr)
                    return answer
                if media.startswith("text/") or "xml" in media:
                    return raw.decode()
                return raw


        """"";
}
