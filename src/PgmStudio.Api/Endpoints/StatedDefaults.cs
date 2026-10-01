using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using NJsonSchema;
using NJsonSchema.Generation;

namespace PgmStudio.Api.Endpoints;

/// <summary>
/// Publishes the default a record states for a field — what a body that leaves the field out is read as.
///
/// <para>A default is published only where the code states one: a positional parameter's <c>= value</c>, or
/// an initializer whose value is not the type's own zero. A plain <c>int</c> with no initializer reads as 0
/// too, but nothing about the field says so on purpose, and publishing it would bury the defaults that are
/// decisions under ones that are not. A closed set's first member is a decision either way, so an enum's
/// initial value is always published. The value is written the way the wire writes it, so an enum's default
/// is its word.</para>
/// </summary>
internal sealed class StatedDefaults : ISchemaProcessor
{
    private static readonly JsonSerializerOptions Wire = Configured();

    public void Process(SchemaProcessorContext context)
    {
        if (context.Schema.HasReference) return;
        var type = context.ContextualType.Type;
        if (type.IsEnum || type.IsPrimitive || type == typeof(string) || type.IsAbstract) return;

        foreach (var (property, value) in Stated(type))
        {
            if (Field(context.Schema, SchemaFields.OnTheWire(property)) is not { } field || field.Default is not null)
                continue;
            field.Default = JsonSerializer.SerializeToNode(value, property.PropertyType, Wire) switch
            {
                JsonValue word when word.TryGetValue<string>(out var text) => text,
                JsonValue whole when whole.TryGetValue<long>(out var count) => count,
                JsonValue number when number.TryGetValue<double>(out var amount) => amount,
                JsonValue flag when flag.TryGetValue<bool>(out var truth) => truth,
                _ => null,
            };
        }
    }

    /// <summary>Every field of <paramref name="type"/> whose default the code states, with that default.</summary>
    private static IEnumerable<(PropertyInfo Property, object Value)> Stated(Type type)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetIndexParameters().Length == 0 && property.GetMethod is { IsPublic: true })
            .ToDictionary(property => property.Name, StringComparer.Ordinal);

        var positional = type.GetConstructors().MaxBy(constructor => constructor.GetParameters().Length);
        foreach (var parameter in positional?.GetParameters() ?? [])
            if (parameter is { HasDefaultValue: true, DefaultValue: { } value, Name: { } name }
                && properties.TryGetValue(name, out var property) && Simple(property.PropertyType))
                yield return (property, Typed(value, property.PropertyType));

        if (type.IsValueType || type.GetConstructor(Type.EmptyTypes) is null) yield break;
        object? instance;
        try { instance = Activator.CreateInstance(type); }
        catch (TargetInvocationException) { yield break; }
        if (instance is null) yield break;

        foreach (var property in properties.Values)
        {
            if (property.SetMethod is null || !Simple(property.PropertyType)) continue;
            var value = property.GetValue(instance);
            if (value is null || Zero(value) || value is "") continue;
            yield return (property, value);
        }
    }

    /// <summary>A parameter's default as the property's own type: reflection hands a closed set's default back
    /// as the number under it.</summary>
    private static object Typed(object value, Type type) =>
        (Nullable.GetUnderlyingType(type) ?? type) is { IsEnum: true } closed && !value.GetType().IsEnum
            ? Enum.ToObject(closed, value)
            : value;

    /// <summary>A value a default can be published as: a word, a number, a flag or a closed set.</summary>
    private static bool Simple(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying.IsPrimitive || underlying.IsEnum || underlying == typeof(string) || underlying == typeof(decimal);
    }

    /// <summary>The type's own zero, which is a default only when a closed set's first member is.</summary>
    private static bool Zero(object value) =>
        !value.GetType().IsEnum && value.GetType().IsValueType && value.Equals(Activator.CreateInstance(value.GetType()));

    /// <summary>The field as the schema holds it: on the schema itself, or on the part of a record that inherits
    /// which carries its own fields.</summary>
    private static JsonSchemaProperty? Field(JsonSchema schema, string name) =>
        schema.Properties.TryGetValue(name, out var own) ? own
        : schema.AllOf.Where(part => !part.HasReference)
            .Select(part => part.Properties.TryGetValue(name, out var field) ? field : null)
            .FirstOrDefault(field => field is not null);

    private static JsonSerializerOptions Configured()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        WireJson.Configure(options);
        return options;
    }
}
