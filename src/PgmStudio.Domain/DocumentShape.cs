using System.Collections;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace PgmStudio.Domain;

/// <summary>
/// Which parts of a posted document the reader did not use.
///
/// <para><b>A field nobody read is the quietest way to lose an authored decision.</b> A misspelled property is
/// dropped by the deserializer and the request answers 200, so a style posted under a wrapper it does not have
/// previews the defaults and reads as working, and a theme whose pattern field is misnamed paints a flat
/// swatch that looks like a previewer limitation. Both cost an author a rebuild cycle to find, and neither is
/// visible from the response.</para>
///
/// <para><b>Why this is a complaint and never a refusal.</b> The readers upgrade stored documents in place —
/// a snapshot outlives the shape it was written in, so <c>HouseStyleJson.Upgrade</c> and
/// <c>TerrainThemeJson.Upgrade</c> carry retired names forward and <see cref="JsonObject.Remove"/> each one
/// they consume. Refusing an unknown property outright would therefore refuse every document written before
/// the last shape change, which is most of them. What is left after an upgrade has run is the honest
/// remainder: a name no record has and no upgrade claimed. It is reported, and the work still succeeds.</para>
///
/// <para>The walk goes down the <b>value</b> rather than the declared type, so a polymorphic member is checked
/// against whatever it actually deserialized to — a <c>voronoi</c> material against the voronoi record — which
/// a walk over declared types could not do.</para>
/// </summary>
public static class DocumentShape
{
    /// <summary>Every property of <paramref name="node"/> that <paramref name="value"/> has nowhere to keep,
    /// as dotted paths in the document's own words (<c>roof.gable.verge</c>, <c>bands[1].materal</c>).
    /// Empty when the document was read whole.</summary>
    public static IReadOnlyList<string> Unread(JsonNode? node, object? value)
    {
        var unread = new List<string>();
        Walk(node, value, "", unread);
        return unread;
    }

    private static void Walk(JsonNode? node, object? value, string path, List<string> unread)
    {
        switch (node)
        {
            // A dictionary's keys are data, not property names — a theme's buckets are named by the author,
            // and reading them as fields would report every one of them as unknown.
            case JsonObject obj when value is IDictionary map:
                foreach (var (key, child) in obj)
                    Walk(child, map.Contains(key) ? map[key] : null, Join(path, key), unread);
                return;

            case JsonObject obj when value is not null:
                var type = value.GetType();
                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                var discriminator = Discriminator(type);
                foreach (var (name, child) in obj)
                {
                    // The discriminator is read by the serializer to choose this very type and is not a
                    // property of it, so a walk over properties alone reports `kind` on every polymorphic
                    // value in every document — the false-positive flood that teaches an author to ignore
                    // the warnings, and then the real one goes past too.
                    if (string.Equals(name, discriminator, StringComparison.OrdinalIgnoreCase)) continue;
                    var property = properties.FirstOrDefault(
                        p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
                    if (property is null) { unread.Add(Join(path, name)); continue; }
                    Walk(child, Read(property, value), Join(path, name), unread);
                }
                return;

            // Zip by position: the reader kept them in order, so element i of the document is element i of
            // the value. A shorter value means entries were dropped, which is a different fault and not this
            // one's to name — see B217, where a voronoi's palette carries only its first entry.
            case JsonArray array when value is IEnumerable items and not string:
                var kept = items.Cast<object?>().ToList();
                for (var i = 0; i < array.Count && i < kept.Count; i++)
                    Walk(array[i], kept[i], $"{path}[{i}]", unread);
                return;
        }
    }

    /// <summary>A property that throws when read tells us nothing about the document, so it is passed over
    /// rather than allowed to fail a read that was only ever asking about field names.</summary>
    private static object? Read(PropertyInfo property, object value)
    {
        if (!property.CanRead || property.GetIndexParameters().Length > 0) return null;
        try { return property.GetValue(value); }
        catch (TargetInvocationException) { return null; }
    }

    /// <summary>The property name a polymorphic hierarchy tags its concrete type with, declared on the base
    /// rather than on the value's own type — so the walk climbs to find it.</summary>
    private static string? Discriminator(Type type)
    {
        for (var t = type; t is not null; t = t.BaseType)
            if (t.GetCustomAttribute<JsonPolymorphicAttribute>() is { } polymorphic)
                return polymorphic.TypeDiscriminatorPropertyName ?? "$type";
        return null;
    }

    private static string Join(string path, string name) => path.Length == 0 ? name : $"{path}.{name}";
}
